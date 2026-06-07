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
    private const string GuardiansPath = "game_state/meta/guardians.json";
    private const string DirectChaosSeaGachaBanner = "direct_chaos_sea";

    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly BrowserLocalWriteCoordinator _coordinator;
    private readonly BrowserAfterlifeTurnRequestQueue _turnRequestQueue;

    public BrowserAfterlifeWriteService(
        FileSystemManager fs,
        StateManager stateManager,
        BrowserLocalWriteCoordinator coordinator)
    {
        _fs = fs;
        _stateManager = stateManager;
        _coordinator = coordinator;
        _turnRequestQueue = new BrowserAfterlifeTurnRequestQueue(fs, stateManager);
    }

    public async Task<BrowserPromptWriteResult> TryApplyAsync(
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var parsed = ParseCommand(command);
        return parsed.Token switch
        {
            "/shining_trade" or "/сияющая_торговля" => await ApplyShiningTradeAsync(parsed.Arguments, answers, owner),
            "/shining_faction_founding" or "/основание_сияющей_фракции" => await ApplyShiningFactionFoundingAsync(answers, owner),
            "/shining_faction_realignment" or "/перестройка_сияющей_фракции" => await ApplyShiningFactionRealignmentAsync(answers, owner),
            "/shining_faction_leadership" or "/смена_главы_сияющей_фракции" => await ApplyShiningFactionLeadershipAsync(answers, owner),
            "/shining_native_faction_discovery" or "/открытие_нативной_фракции" => await ApplyShiningNativeFactionDiscoveryAsync(answers, owner),
            "/shining_faction_investment" or "/инвестиция_в_сияющую_фракцию" => await ApplyShiningFactionInvestmentAsync(parsed.Arguments, answers, owner),
            "/shining_project_support" or "/поддержать_сияющий_проект" => await ApplyShiningProjectSupportMutationAsync(answers, owner, support: true),
            "/shining_project_unsupport" or "/снять_поддержку_сияющего_проекта" => await ApplyShiningProjectSupportMutationAsync(answers, owner, support: false),
            "/shining_project_retirement" or "/отправить_сияющий_проект_в_историю" => await ApplyShiningProjectRetirementAsync(answers, owner),
            "/shining_treasury" or "/казначейство" => await ApplyShiningTreasuryAsync(answers, owner),
            "/source_of_light" or "/источник_света" => await ApplySourceOfLightAsync(answers, owner),
            "/afterlife_inbox" or "/уведомления_загробья" => await ApplyAfterlifeInboxAsync(answers, owner),
            "/spiritual_arts" or "/духовные_искусства" => await ApplySpiritualArtsAsync(answers, owner),
            "/spiritual_action" or "/духовное_действие" => await BuildSpiritualActionPayloadAsync(answers),
            "/gacha" or "/гача" => string.IsNullOrWhiteSpace(parsed.Arguments)
                ? await ApplyGachaPullAsync(answers, owner)
                : BrowserPromptWriteResult.ValidationError("Команда /gacha не принимает аргументы. Выберите поддерживаемый прямой призыв Моря Хаоса через браузерную форму."),
            "/abode_offering" or "/подношение_обители" => await ApplyAbodeOfferingAsync(answers, owner),
            "/found_guardian_mantle" or "/учредить_хранителя" => await ApplyPlayerGuardianFoundationAsync(answers, owner),
            "/guardian_trade" or "/торговля_хранителя" => await ApplyGuardianTradeAsync(parsed.Arguments, answers, owner),
            "/guardian_social" or "/talk_guardian" or "/поговорить_с_хранителем" or "/общение_хранителя" => await ApplyGuardianSocialAsync(parsed.Arguments, answers, owner),
            "/abode_residents" or "/обитатели_обители" => await ApplyAbodeResidentsAsync(parsed.Arguments, answers, owner),
            "/resident_interaction" or "/общение_резидента" or "/поговорить_с_резидентом" or "/история_резидента" => await ApplyResidentInteractionAsync(parsed.Arguments, answers, owner),
            "/resident_transfer" or "/переход_резидента" => await ApplyResidentTransferAsync(parsed.Arguments, answers, owner),
            "/soul_relic_equip" or "/экипировать_реликвию" => await ApplySoulRelicEquipAsync(answers, owner),
            "/soul_relic_unequip" or "/снять_реликвию" => await ApplySoulRelicUnequipAsync(answers, owner),
            _ => BrowserPromptWriteResult.NotHandled()
        };
    }

    private async Task<BrowserPromptWriteResult> ApplyShiningTradeAsync(
        string commandArguments,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_trade_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите сделку.");

        var factionId = string.IsNullOrWhiteSpace(commandArguments)
            ? ReadAnswer(answers, "faction_id")
            : commandArguments.Trim();
        if (string.IsNullOrWhiteSpace(factionId))
            return BrowserPromptWriteResult.ValidationError("Выберите сияющую фракцию.");

        var choice = ReadAnswer(answers, "shining_trade_choice");
        if (!TryParseTradeChoice(choice, out var operation, out var targetId))
            return BrowserPromptWriteResult.ValidationError("Выберите запрос витрины или покупку.");
        if (operation == "request" && string.Equals(targetId, "__selected__", StringComparison.OrdinalIgnoreCase))
            targetId = factionId;
        if (operation == "sell")
            return BrowserPromptWriteResult.ValidationError("Продажа сияющим фракциям пока не поддержана текущими правилами торговли.");
        if (operation is not ("request" or "buy"))
            return BrowserPromptWriteResult.ValidationError("Сияющая торговля поддерживает запрос витрины и покупку из готовой витрины.");

        await _stateManager.RefreshGameStateAsync();
        var currentTurn = Math.Max(1, _stateManager.CurrentState.TurnNumber);
        var rollbackPaths = operation == "buy"
            ? new[] { SoulStatePath, ShiningAbodeState.StatePath, ShiningTradeRequestState.PendingRequestsPath }
            : new[] { ShiningTradeRequestState.PendingRequestsPath, ShiningAbodeState.StatePath };

        return await ExecuteAsync(
            owner,
            "Сияющая торговля",
            rollbackPaths,
            async () =>
            {
                var result = operation switch
                {
                    "request" => await ShiningTradeService.RequestInventoryAsync(_fs, targetId, currentTurn),
                    "buy" => await ShiningTradeService.BuyAsync(_fs, factionId, targetId, currentTurn),
                    _ => new ShiningTradeService.ShiningTradeOperationResult(false, false, "Выберите поддерживаемое действие.")
                };

                if (!result.Success)
                    throw new InvalidOperationException(result.Message);
                await _stateManager.RefreshGameStateAsync();
            },
            operation == "request" ? "Витрина запрошена" : "Покупка завершена",
            operation == "request"
                ? "Запрос ассортимента сияющей фракции отправлен."
                : "Покупка из сияющей витрины выполнена.",
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyGuardianTradeAsync(
        string commandArguments,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_trade_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите сделку.");

        var guardianId = string.IsNullOrWhiteSpace(commandArguments)
            ? ReadAnswer(answers, "guardian_id")
            : commandArguments.Trim();
        if (string.IsNullOrWhiteSpace(guardianId))
            return BrowserPromptWriteResult.ValidationError("Выберите Хранителя.");

        var choice = ReadAnswer(answers, "guardian_trade_choice");
        if (!TryParseTradeChoice(choice, out var operation, out var targetId))
            return BrowserPromptWriteResult.ValidationError("Выберите запрос витрины, покупку, продажу или обратный выкуп.");
        if (operation == "request" && string.Equals(targetId, "__selected__", StringComparison.OrdinalIgnoreCase))
            targetId = guardianId;

        await _stateManager.RefreshGameStateAsync();
        var currentTurn = Math.Max(1, _stateManager.CurrentState.TurnNumber);
        var currentIncarnation = Math.Max(1, _stateManager.CurrentState.Incarnation);
        var service = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        var rollbackPaths = operation switch
        {
            "request" => new[] { GuardianTradeRequestState.PendingRequestPath, "game_state/meta/guardians.json", GuardianProjectState.TrackerPath },
            "buy" => new[] { SoulStatePath, "game_state/meta/guardians.json", GuardianProjectState.TrackerPath, GuardianTradeRequestState.PendingRequestPath },
            _ => new[] { SoulStatePath, "game_state/meta/guardians.json" }
        };

        return await ExecuteAsync(
            owner,
            "Торговля хранителя",
            rollbackPaths,
            async () =>
            {
                var result = operation switch
                {
                    "request" => await BuildGuardianTradeRequestResultAsync(service, targetId, currentIncarnation, currentTurn),
                    "buy" => await service.BuyAsync(guardianId, targetId, currentIncarnation, currentTurn),
                    "sell" => await service.SellAsync(guardianId, targetId, currentTurn),
                    "buyback" => await service.BuyBackAsync(guardianId, targetId, currentTurn),
                    _ => new GuardianTradeService.GuardianTradeOperationResult(false, false, "Выберите поддерживаемую сделку.")
                };

                if (!result.Success)
                    throw new InvalidOperationException(result.Message);
                await _stateManager.RefreshGameStateAsync();
            },
            operation == "request" ? "Витрина запрошена" : "Торговля завершена",
            operation switch
            {
                "request" => "Запрос ассортимента Хранителя отправлен.",
                "buy" => "Покупка у Хранителя выполнена.",
                "sell" => "Продажа Хранителю выполнена.",
                "buyback" => "Обратный выкуп у Хранителя выполнен.",
                _ => "Сделка выполнена."
            },
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyGuardianSocialAsync(
        string commandArguments,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        await _stateManager.RefreshGameStateAsync();
        var soulRoot = await ReadObjectAsync(SoulStatePath);
        var currentRealm = FirstNonEmpty(GetNodeString(soulRoot?["currentRealm"]), _stateManager.CurrentState.CurrentRealm);
        if (!RealmSemantics.IsAfterlifeRealm(currentRealm))
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Blocked,
                UiNotificationSeverity.Warning,
                "Общение с Хранителем недоступно",
                "Разговор или просьбу о знаниях можно отправить только в посмертии. Сейчас действие недоступно для текущего царства.");
        }

        var guardianId = ReadAnswer(answers, "guardian_id");
        if (string.IsNullOrWhiteSpace(guardianId))
            guardianId = commandArguments.Trim();
        if (string.IsNullOrWhiteSpace(guardianId))
            return BrowserPromptWriteResult.ValidationError("Выберите Хранителя.");

        var interactionType = ReadAnswer(answers, "guardian_interaction_type").Trim().ToLowerInvariant();
        if (!ActorSocialInteractionRequestState.IsSupportedGuardianInteractionType(interactionType))
            return BrowserPromptWriteResult.ValidationError("Выберите тип обращения: разговор или знания.");

        var guardiansRoot = await ReadObjectAsync("game_state/meta/guardians.json");
        if (guardiansRoot == null)
            return BrowserPromptWriteResult.ValidationError("Список Хранителей сейчас недоступен.");

        var guardian = FindGuardianByIdOrName(guardiansRoot, guardianId);
        if (guardian == null)
            return BrowserPromptWriteResult.ValidationError("Такого Хранителя сейчас нет среди известных.");

        var manifestation = guardian["manifestation"] as JsonObject;
        var stableGuardianId = FirstNonEmpty(GetNodeString(guardian["guardianId"]), GetNodeString(guardian["id"]), guardianId);
        var guardianName = FirstNonEmpty(
            GetNodeString(guardian["canonicalName"]),
            GetNodeString(guardian["guardianName"]),
            GetNodeString(guardian["name"]),
            GetNodeString(manifestation?["currentDisplayName"]),
            GetNodeString(guardian["displayName"]),
            stableGuardianId);

        var pendingState = await ActorSocialInteractionRequestState.ReadGuardianRequestsStateAsync(_fs);
        if (pendingState.IsMalformed)
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Failed,
                UiNotificationSeverity.Error,
                "Обращение не отправлено",
                "Запрос общения временно ждёт проверки состояния. Повторите действие после восстановления игрового состояния.");
        }

        if (HasPendingGuardianSocialRequest(pendingState.Requests, stableGuardianId, interactionType))
            return BuildDuplicateGuardianSocialResult(guardianName, interactionType);

        var currentTurn = Math.Max(1, _stateManager.CurrentState.TurnNumber);
        var request = new ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest
        {
            GuardianId = stableGuardianId,
            GuardianName = guardianName,
            InteractionType = interactionType,
            CreatedAtTurn = currentTurn
        };

        var duplicateDuringWrite = false;
        var writeResult = await ExecuteAsync(
            owner,
            "Общение с Хранителем",
            [ActorSocialInteractionRequestState.PendingGuardianRequestPath],
            async () =>
            {
                var state = await ActorSocialInteractionRequestState.ReadGuardianRequestsStateAsync(_fs);
                if (state.IsMalformed)
                    throw new InvalidOperationException("Запрос общения временно ждёт проверки состояния.");

                duplicateDuringWrite = HasPendingGuardianSocialRequest(state.Requests, stableGuardianId, interactionType);
                if (!duplicateDuringWrite)
                    await ActorSocialInteractionRequestState.WriteGuardianRequestAsync(_fs, request);
            },
            interactionType == ActorSocialInteractionRequestState.GuardianInteractionTypeLore
                ? "Просьба о знаниях отправлена ГМ"
                : "Разговор отправлен ГМ",
            interactionType == ActorSocialInteractionRequestState.GuardianInteractionTypeLore
                ? $"ГМ получит просьбу о знаниях от Хранителя {guardianName}."
                : $"ГМ получит запрос разговора с Хранителем {guardianName}.",
            payload: null);

        return duplicateDuringWrite
            ? BuildDuplicateGuardianSocialResult(guardianName, interactionType)
            : writeResult;
    }

    private static BrowserPromptWriteResult BuildDuplicateGuardianSocialResult(string guardianName, string interactionType)
    {
        var isLore = string.Equals(interactionType, ActorSocialInteractionRequestState.GuardianInteractionTypeLore, StringComparison.OrdinalIgnoreCase);
        return BrowserPromptWriteResult.Failed(
            CommandExecutionState.Pending,
            UiNotificationSeverity.Warning,
            isLore ? "Просьба о знаниях уже ожидает ГМ" : "Разговор уже ожидает ГМ",
            isLore
                ? $"Просьба о знаниях для Хранителя {guardianName} уже ожидает ответа ГМ. Дождитесь результата, затем отправьте новую просьбу."
                : $"Разговор с Хранителем {guardianName} уже ожидает ответа ГМ. Дождитесь результата, затем начните новый разговор.");
    }

    private static bool HasPendingGuardianSocialRequest(
        IEnumerable<ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest> requests,
        string guardianId,
        string interactionType) =>
        requests.Any(request =>
            string.Equals(request.GuardianId, guardianId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.InteractionType, interactionType, StringComparison.OrdinalIgnoreCase));

    private async Task<BrowserPromptWriteResult> ApplyAbodeResidentsAsync(
        string commandArguments,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        await _stateManager.RefreshGameStateAsync();
        var realmBlocker = await TryBuildResidentRealmBlockerAsync("Обитатели Обители недоступны");
        if (realmBlocker != null)
            return realmBlocker;

        var selection = ReadAnswer(answers, "guardian_abode_id");
        if (string.IsNullOrWhiteSpace(selection))
            selection = commandArguments.Trim();
        if (string.IsNullOrWhiteSpace(selection))
            return BrowserPromptWriteResult.ValidationError("Выберите Обитель.");

        var guardiansRoot = await TryReadObjectSafeAsync("game_state/meta/guardians.json");
        if (guardiansRoot == null)
            return BrowserPromptWriteResult.ValidationError("Список Хранителей сейчас недоступен.");

        var abode = ResolveGuardianAbodeOption(CollectGuardianAbodeOptions(guardiansRoot).ToList(), selection);
        if (abode == null)
            return BrowserPromptWriteResult.ValidationError("Такой Обители сейчас нет среди известных.");

        if (await GuardianAbodeResidentRequestState.IsResidentsRequestFileMalformedAsync(_fs))
            return BuildMalformedResidentPendingResult("Запрос состава не отправлен");

        var existingRequests = await GuardianAbodeResidentRequestState.ReadResidentsRequestsAsync(_fs);
        if (HasPendingResidentsRequest(existingRequests, abode.GuardianId, abode.AbodeId))
            return BuildDuplicateResidentsRequestResult(abode);

        var isFoundedGuardian = PlayerGuardianFoundationState.IsPlayerFoundedGuardian(abode.Guardian);
        var request = new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest
        {
            GuardianId = abode.GuardianId,
            GuardianName = abode.GuardianName,
            AbodeId = abode.AbodeId,
            AbodeName = abode.AbodeName,
            CurrentReputation = abode.CurrentReputation,
            RequestMode = isFoundedGuardian
                ? GuardianAbodeResidentRequestState.ResidentsRequestModeFounderAttraction
                : GuardianAbodeResidentRequestState.ResidentsRequestModeStandardRoster,
            FounderFeatureTitle = isFoundedGuardian ? PlayerGuardianFoundationState.GetFounderAbodeFeatureTitle(abode.Guardian) : null,
            FounderFeatureSummary = isFoundedGuardian ? PlayerGuardianFoundationState.GetFounderAbodeFeatureSummary(abode.Guardian) : null,
            CreatedAtTurn = Math.Max(0, _stateManager.CurrentState.TurnNumber)
        };

        var duplicateDuringWrite = false;
        var writeResult = await ExecuteAsync(
            owner,
            "Обитатели Обители",
            [GuardianAbodeResidentRequestState.PendingResidentsRequestPath],
            async () =>
            {
                if (await GuardianAbodeResidentRequestState.IsResidentsRequestFileMalformedAsync(_fs))
                    throw new InvalidOperationException("Запрос состава временно ждёт проверки состояния.");

                var currentRequests = await GuardianAbodeResidentRequestState.ReadResidentsRequestsAsync(_fs);
                duplicateDuringWrite = HasPendingResidentsRequest(currentRequests, abode.GuardianId, abode.AbodeId);
                if (!duplicateDuringWrite)
                    await GuardianAbodeResidentRequestState.WriteResidentsRequestAsync(_fs, request);
            },
            "Запрос состава отправлен ГМ",
            $"ГМ получит просьбу подготовить состав Обители {abode.AbodeName} Хранителя {abode.GuardianName}.",
            payload: null);

        return duplicateDuringWrite ? BuildDuplicateResidentsRequestResult(abode) : writeResult;
    }

    private async Task<BrowserPromptWriteResult> ApplyResidentInteractionAsync(
        string commandArguments,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        await _stateManager.RefreshGameStateAsync();
        var realmBlocker = await TryBuildResidentRealmBlockerAsync("Общение с обитателем недоступно");
        if (realmBlocker != null)
            return realmBlocker;

        var residentId = ReadAnswer(answers, "resident_id");
        if (string.IsNullOrWhiteSpace(residentId))
            residentId = commandArguments.Trim();
        if (string.IsNullOrWhiteSpace(residentId))
            return BrowserPromptWriteResult.ValidationError("Выберите обитателя.");

        var interactionType = ReadAnswer(answers, "resident_interaction_type").Trim().ToLowerInvariant();
        if (interactionType is not (GuardianAbodeResidentState.InteractionTypeTalk or GuardianAbodeResidentState.InteractionTypeHistory))
            return BrowserPromptWriteResult.ValidationError("Выберите разговор или раскрытие истории.");

        var context = await ReadResidentWriteContextAsync();
        if (!string.IsNullOrWhiteSpace(context.ErrorMessage))
            return BrowserPromptWriteResult.ValidationError(context.ErrorMessage);

        var resident = ResolveResidentWriteOption(context.Residents, residentId);
        if (resident == null)
            return BrowserPromptWriteResult.ValidationError("Такого обитателя сейчас нет среди состава Обители.");
        if (!resident.Entry.IsPresent)
            return BrowserPromptWriteResult.ValidationError("Этот обитатель сейчас не находится в Обители.");
        if (!ResidentInteractionAllowed(resident.Entry, interactionType))
            return BrowserPromptWriteResult.ValidationError("Этот тип обращения сейчас недоступен для выбранного обитателя.");

        if (await GuardianAbodeResidentRequestState.IsInteractionRequestFileMalformedAsync(_fs))
            return BuildMalformedResidentPendingResult("Обращение не отправлено");

        var existingRequests = await GuardianAbodeResidentRequestState.ReadInteractionRequestsAsync(_fs);
        if (HasPendingInteractionRequest(existingRequests, resident.Entry.ResidentId, interactionType))
            return BuildDuplicateInteractionRequestResult(resident, interactionType);

        var request = new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest
        {
            GuardianId = resident.Entry.GuardianId,
            GuardianName = resident.GuardianName,
            AbodeId = resident.Entry.AbodeId,
            AbodeName = resident.AbodeName,
            ResidentId = resident.Entry.ResidentId,
            ResidentName = resident.Entry.DisplayName,
            InteractionType = interactionType,
            CreatedAtTurn = Math.Max(0, _stateManager.CurrentState.TurnNumber)
        };

        var duplicateDuringWrite = false;
        var writeResult = await ExecuteAsync(
            owner,
            "Общение с обитателем",
            [GuardianAbodeResidentRequestState.PendingInteractionsRequestPath],
            async () =>
            {
                if (await GuardianAbodeResidentRequestState.IsInteractionRequestFileMalformedAsync(_fs))
                    throw new InvalidOperationException("Обращение временно ждёт проверки состояния.");

                var currentRequests = await GuardianAbodeResidentRequestState.ReadInteractionRequestsAsync(_fs);
                duplicateDuringWrite = HasPendingInteractionRequest(currentRequests, resident.Entry.ResidentId, interactionType);
                if (!duplicateDuringWrite)
                    await GuardianAbodeResidentRequestState.WriteInteractionRequestAsync(_fs, request);
            },
            string.Equals(interactionType, GuardianAbodeResidentState.InteractionTypeHistory, StringComparison.OrdinalIgnoreCase)
                ? "Просьба об истории отправлена ГМ"
                : "Разговор отправлен ГМ",
            string.Equals(interactionType, GuardianAbodeResidentState.InteractionTypeHistory, StringComparison.OrdinalIgnoreCase)
                ? $"ГМ получит просьбу раскрыть историю обитателя {resident.Entry.DisplayName}."
                : $"ГМ получит запрос разговора с обитателем {resident.Entry.DisplayName}.",
            payload: null);

        return duplicateDuringWrite ? BuildDuplicateInteractionRequestResult(resident, interactionType) : writeResult;
    }

    private async Task<BrowserPromptWriteResult> ApplyResidentTransferAsync(
        string commandArguments,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        await _stateManager.RefreshGameStateAsync();
        var realmBlocker = await TryBuildResidentRealmBlockerAsync("Переход обитателя недоступен");
        if (realmBlocker != null)
            return realmBlocker;

        var residentId = ReadAnswer(answers, "resident_id");
        if (string.IsNullOrWhiteSpace(residentId))
            residentId = commandArguments.Trim();
        if (string.IsNullOrWhiteSpace(residentId))
            return BrowserPromptWriteResult.ValidationError("Выберите обитателя.");

        var transferChoice = ReadAnswer(answers, "resident_transfer_choice");
        if (string.IsNullOrWhiteSpace(transferChoice))
            return BrowserPromptWriteResult.ValidationError("Выберите направление перехода.");

        var context = await ReadResidentWriteContextAsync();
        if (!string.IsNullOrWhiteSpace(context.ErrorMessage))
            return BrowserPromptWriteResult.ValidationError(context.ErrorMessage);

        var resident = ResolveResidentWriteOption(context.Residents, residentId);
        if (resident == null)
            return BrowserPromptWriteResult.ValidationError("Такого обитателя сейчас нет среди состава Обители.");
        if (!resident.Entry.IsPresent)
            return BrowserPromptWriteResult.ValidationError("Этот обитатель сейчас не находится в Обители.");
        if (!string.Equals(resident.Entry.MigrationState, GuardianAbodeResidentState.MigrationStateReadyToTransfer, StringComparison.OrdinalIgnoreCase))
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Blocked,
                UiNotificationSeverity.Warning,
                "Переход не отправлен",
                $"Обитатель {resident.Entry.DisplayName} ещё не готов к переходу. Дождитесь явного состояния готовности.");
        }

        if (await GuardianAbodeResidentRequestState.IsTransferRequestFileMalformedAsync(_fs))
            return BuildMalformedResidentPendingResult("Переход не отправлен");

        var existingTransfer = await GuardianAbodeResidentRequestState.FindPendingTransferAsync(_fs, resident.Entry.ResidentId);
        if (existingTransfer != null)
            return BuildDuplicateTransferRequestResult(resident);

        if (!TryBuildTransferChoice(transferChoice, resident, context.GuardiansRoot, context.ResidentsRoot, out var transferRequest, out var validationMessage))
            return BrowserPromptWriteResult.ValidationError(validationMessage);
        transferRequest.CreatedAtTurn = Math.Max(0, _stateManager.CurrentState.TurnNumber);

        var duplicateDuringWrite = false;
        var writeResult = await ExecuteAsync(
            owner,
            "Переход обитателя",
            [GuardianAbodeResidentRequestState.PendingTransfersRequestPath],
            async () =>
            {
                if (await GuardianAbodeResidentRequestState.IsTransferRequestFileMalformedAsync(_fs))
                    throw new InvalidOperationException("Переход временно ждёт проверки состояния.");

                var currentTransfer = await GuardianAbodeResidentRequestState.FindPendingTransferAsync(_fs, resident.Entry.ResidentId);
                duplicateDuringWrite = currentTransfer != null;
                if (!duplicateDuringWrite)
                    await GuardianAbodeResidentRequestState.WriteTransferRequestAsync(_fs, transferRequest);
            },
            "Переход отправлен ГМ",
            string.Equals(transferRequest.TransferMode, GuardianAbodeResidentState.TransferModeDepartureOnly, StringComparison.OrdinalIgnoreCase)
                ? $"ГМ получит просьбу отпустить обитателя {resident.Entry.DisplayName} без новой Обители."
                : $"ГМ получит просьбу о переходе обитателя {resident.Entry.DisplayName} в Обитель {transferRequest.TargetAbodeName}.",
            payload: null);

        return duplicateDuringWrite ? BuildDuplicateTransferRequestResult(resident) : writeResult;
    }

    private async Task<BrowserPromptWriteResult?> TryBuildResidentRealmBlockerAsync(string title)
    {
        JsonObject? soulRoot = null;
        try
        {
            soulRoot = await ReadObjectAsync(SoulStatePath);
        }
        catch
        {
            // Fall back to StateManager's resolved realm below.
        }

        var currentRealm = FirstNonEmpty(GetNodeString(soulRoot?["currentRealm"]), _stateManager.CurrentState.CurrentRealm);
        if (RealmSemantics.IsAfterlifeRealm(currentRealm))
            return null;

        return BrowserPromptWriteResult.Failed(
            CommandExecutionState.Blocked,
            UiNotificationSeverity.Warning,
            title,
            "Действия с обитателями Обители доступны только в посмертии. Сейчас действие недоступно для текущего царства.");
    }

    private static BrowserPromptWriteResult BuildMalformedResidentPendingResult(string title) =>
        BrowserPromptWriteResult.Failed(
            CommandExecutionState.Failed,
            UiNotificationSeverity.Error,
            title,
            "Запрос временно ждёт проверки состояния. Повторите действие после восстановления игрового состояния.");

    private static BrowserPromptWriteResult BuildDuplicateResidentsRequestResult(GuardianAbodeBrowserOption abode) =>
        BrowserPromptWriteResult.Failed(
            CommandExecutionState.Pending,
            UiNotificationSeverity.Warning,
            "Запрос состава уже ожидает ГМ",
            $"Запрос состава Обители {abode.AbodeName} Хранителя {abode.GuardianName} уже ожидает ответа ГМ. Дождитесь результата, затем отправьте новый запрос.");

    private static BrowserPromptWriteResult BuildDuplicateInteractionRequestResult(ResidentWriteOption resident, string interactionType)
    {
        var isHistory = string.Equals(interactionType, GuardianAbodeResidentState.InteractionTypeHistory, StringComparison.OrdinalIgnoreCase);
        return BrowserPromptWriteResult.Failed(
            CommandExecutionState.Pending,
            UiNotificationSeverity.Warning,
            isHistory ? "Просьба об истории уже ожидает ГМ" : "Разговор уже ожидает ГМ",
            isHistory
                ? $"Просьба об истории обитателя {resident.Entry.DisplayName} уже ожидает ответа ГМ. Дождитесь результата, затем отправьте новую просьбу."
                : $"Разговор с обитателем {resident.Entry.DisplayName} уже ожидает ответа ГМ. Дождитесь результата, затем начните новый разговор.");
    }

    private static BrowserPromptWriteResult BuildDuplicateTransferRequestResult(ResidentWriteOption resident) =>
        BrowserPromptWriteResult.Failed(
            CommandExecutionState.Pending,
            UiNotificationSeverity.Warning,
            "Переход уже ожидает ГМ",
            $"Переход обитателя {resident.Entry.DisplayName} уже ожидает ответа ГМ. Дождитесь результата перед новым запросом.");

    private static bool HasPendingResidentsRequest(
        IEnumerable<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest> requests,
        string guardianId,
        string abodeId) =>
        requests.Any(request =>
            string.Equals(request.GuardianId, guardianId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.AbodeId, abodeId, StringComparison.OrdinalIgnoreCase));

    private static bool HasPendingInteractionRequest(
        IEnumerable<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest> requests,
        string residentId,
        string interactionType) =>
        requests.Any(request =>
            string.Equals(request.ResidentId, residentId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.InteractionType, interactionType, StringComparison.OrdinalIgnoreCase));

    private async Task<BrowserPromptWriteResult> ApplyShiningFactionFoundingAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_shining_politics_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите основание сияющей фракции.");

        await _stateManager.RefreshGameStateAsync();
        var realmBlocker = await TryBuildShiningPoliticsRealmBlockerAsync("Основание сияющей фракции недоступно");
        if (realmBlocker != null)
            return realmBlocker;

        var factionName = ReadAnswer(answers, "faction_name");
        var hallName = ReadAnswer(answers, "hall_name");
        var charterSummary = ReadAnswer(answers, "charter_summary");
        var hallDescription = ReadAnswer(answers, "hall_description");
        var favoredArchetype = ReadAnswer(answers, "favored_archetype");
        var patronEffectFamily = ReadAnswer(answers, "patron_effect_family");
        var secondaryTag = ReadAnswer(answers, "hall_secondary_service_tag");
        var supporterIds = ParseIdList(ReadAnswer(answers, "supporting_resident_ids"));

        if (string.IsNullOrWhiteSpace(factionName) ||
            string.IsNullOrWhiteSpace(hallName) ||
            string.IsNullOrWhiteSpace(charterSummary) ||
            string.IsNullOrWhiteSpace(hallDescription))
        {
            return BrowserPromptWriteResult.ValidationError("Заполните название фракции, название зала, хартию и описание зала.");
        }

        if (!ShiningAbodeState.IsSupportedProjectArchetype(favoredArchetype))
            return BrowserPromptWriteResult.ValidationError("Выберите поддерживаемый архетип проектов.");
        if (!ShiningAbodeState.IsSupportedEffectFamily(patronEffectFamily))
            return BrowserPromptWriteResult.ValidationError("Выберите поддерживаемую семью эффекта.");
        if (supporterIds.Count < 3)
            return BrowserPromptWriteResult.ValidationError("Для основания нужны минимум три уникальных сторонника.");

        return await ExecuteAsync(
            owner,
            "Основание сияющей фракции",
            [SoulStatePath, ShiningAbodeState.StatePath, GuardianAbodeResidentState.StatePath, ShiningFactionRequestState.PendingFoundingsRequestPath],
            async () =>
            {
                var soulRoot = await ReadRequiredObjectAsync(SoulStatePath, "Состояние души сейчас недоступно.");
                var shiningRoot = await ReadRequiredObjectAsync(ShiningAbodeState.StatePath, "Состояние Сияющей Обители сейчас недоступно.");
                var residentsRoot = await ReadRequiredObjectAsync(GuardianAbodeResidentState.StatePath, "Состав обитателей сейчас недоступен.");
                if (supporterIds.Any(supporterId => !IsVisibleAscendedResidentForPolitics(shiningRoot, residentsRoot, supporterId, allowFactionless: true)))
                    throw new InvalidOperationException("Выберите видимых вознесённых сторонников, доступных в политике Сияющей Обители.");

                var feathersBefore = GetSoulInkFeathers(soulRoot);
                var sparksBefore = GetNodeInt(shiningRoot["lightSparks"]);
                if (feathersBefore < ShiningFactionRequestState.FactionFoundingCostFeathers ||
                    sparksBefore < ShiningFactionRequestState.FactionFoundingCostLightSparks)
                {
                    throw new InvalidOperationException($"Недостаточно ресурсов: нужно {ShiningFactionRequestState.FactionFoundingCostFeathers} Чернильных Перьев и {ShiningFactionRequestState.FactionFoundingCostLightSparks} Искр Света.");
                }

                var request = new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
                {
                    ProposedFactionId = BuildSlugId("faction", factionName),
                    ProposedHallId = BuildSlugId("hall", hallName),
                    ProposedHallName = hallName.Trim(),
                    ProposedHallDescription = hallDescription.Trim(),
                    ProposedHallServiceTags = BuildFoundingServiceTags(patronEffectFamily, secondaryTag),
                    Charter = new ShiningFactionRequestState.FactionCharterPayload
                    {
                        FactionName = factionName.Trim(),
                        FavoredArchetype = favoredArchetype.Trim(),
                        PatronEffectFamily = patronEffectFamily.Trim(),
                        Summary = charterSummary.Trim()
                    },
                    SupportingResidentIds = supporterIds,
                    QuotedCostFeathers = ShiningFactionRequestState.FactionFoundingCostFeathers,
                    QuotedCostLightSparks = ShiningFactionRequestState.FactionFoundingCostLightSparks,
                    ReservedInkFeathersBefore = feathersBefore,
                    ReservedLightSparksBefore = sparksBefore,
                    CreatedAtTurn = Math.Max(1, _stateManager.CurrentState.TurnNumber + 1)
                };

                var validation = await ShiningFactionRequestState.ValidateFoundingRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(validation))
                    throw new InvalidOperationException(SanitizeShiningPoliticsValidationMessage(validation));

                await ShiningFactionRequestState.WriteFoundingRequestAsync(_fs, request);
                SetSoulInkFeathers(soulRoot, feathersBefore - ShiningFactionRequestState.FactionFoundingCostFeathers);
                shiningRoot["lightSparks"] = sparksBefore - ShiningFactionRequestState.FactionFoundingCostLightSparks;
                await WriteObjectAsync(SoulStatePath, soulRoot);
                await WriteObjectAsync(ShiningAbodeState.StatePath, shiningRoot);

                var postValidation = await ShiningFactionRequestState.ValidateFoundingRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(postValidation))
                    throw new InvalidOperationException(SanitizeShiningPoliticsValidationMessage(postValidation));
                await _stateManager.RefreshGameStateAsync();
            },
            "Основание отправлено ГМ",
            $"Запрос основания фракции {factionName.Trim()} отправлен. Ресурсы зарезервированы до ответа ГМ.",
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyShiningFactionRealignmentAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_shining_politics_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите запрос перестройки.");

        await _stateManager.RefreshGameStateAsync();
        var realmBlocker = await TryBuildShiningPoliticsRealmBlockerAsync("Перестройка сияющей фракции недоступна");
        if (realmBlocker != null)
            return realmBlocker;

        var residentId = ReadAnswer(answers, "resident_id");
        var mode = ReadAnswer(answers, "realignment_mode").Trim().ToLowerInvariant();
        var targetFactionId = ReadAnswer(answers, "target_faction_id");
        if (string.IsNullOrWhiteSpace(residentId))
            return BrowserPromptWriteResult.ValidationError("Выберите обитателя для перестройки.");
        if (mode is not (ShiningFactionRequestState.RealignmentModeAcceptedTransfer or ShiningFactionRequestState.RealignmentModeDepartureToNeutral))
            return BrowserPromptWriteResult.ValidationError("Выберите переход в другую фракцию или нейтралитет.");
        if (string.Equals(mode, ShiningFactionRequestState.RealignmentModeAcceptedTransfer, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(targetFactionId))
        {
            return BrowserPromptWriteResult.ValidationError("Для перехода выберите целевую фракцию.");
        }

        return await ExecuteAsync(
            owner,
            "Перестройка сияющей фракции",
            [ShiningFactionRequestState.PendingRealignmentsRequestPath, GuardianAbodeResidentState.StatePath, ShiningAbodeState.StatePath],
            async () =>
            {
                var shiningRoot = await ReadRequiredObjectAsync(ShiningAbodeState.StatePath, "Состояние Сияющей Обители сейчас недоступно.");
                var residentsRoot = await ReadRequiredObjectAsync(GuardianAbodeResidentState.StatePath, "Состав обитателей сейчас недоступен.");
                var resident = FindResident(residentsRoot, residentId);
                if (resident == null)
                    throw new InvalidOperationException("Такого обитателя сейчас нет среди состава Сияющей Обители.");

                var sourceFactionId = GetNodeString(resident["shiningFactionId"]) ?? string.Empty;
                var sourceFaction = FindVisibleOperationalFaction(shiningRoot, sourceFactionId);
                if (sourceFaction == null)
                    throw new InvalidOperationException("Выбранный обитатель больше не принадлежит видимой действующей фракции Сияющей Обители.");

                var request = new ShiningFactionRequestState.PendingShiningFactionRealignmentRequest
                {
                    ResidentId = GetNodeString(resident["residentId"]) ?? residentId,
                    ResidentName = GetResidentName(resident),
                    SourceFactionId = sourceFactionId,
                    SourceFactionName = ResolveFactionName(sourceFaction, sourceFactionId),
                    RealignmentMode = mode,
                    FactionLoyaltyLevel = GetNodeInt(resident["factionLoyaltyLevel"]),
                    FactionLoyaltyTier = GetNodeString(resident["factionLoyaltyTier"]) ?? ShiningAbodeState.ResolveFactionLoyaltyTier(GetNodeInt(resident["factionLoyaltyLevel"])),
                    FactionRestlessness = GetNodeInt(resident["factionRestlessness"]),
                    FactionRealignmentState = GetNodeString(resident["factionRealignmentState"]) ?? ShiningAbodeState.ResolveFactionRealignmentState(GetNodeInt(resident["factionLoyaltyLevel"]), GetNodeInt(resident["factionRestlessness"])),
                    CreatedAtTurn = Math.Max(1, _stateManager.CurrentState.TurnNumber + 1)
                };

                if (string.Equals(mode, ShiningFactionRequestState.RealignmentModeAcceptedTransfer, StringComparison.OrdinalIgnoreCase))
                {
                    var targetFaction = FindVisibleOperationalFaction(shiningRoot, targetFactionId);
                    if (targetFaction == null)
                        throw new InvalidOperationException("Выберите видимую действующую целевую фракцию Сияющей Обители.");
                    request.TargetFactionId = targetFactionId.Trim();
                    request.TargetFactionName = ResolveFactionName(targetFaction, targetFactionId.Trim());
                }

                var validation = await ShiningFactionRequestState.ValidateRealignmentRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(validation))
                    throw new InvalidOperationException(SanitizeShiningPoliticsValidationMessage(validation));

                await ShiningFactionRequestState.WriteRealignmentRequestAsync(_fs, request);
                await _stateManager.RefreshGameStateAsync();
            },
            "Перестройка отправлена ГМ",
            "Запрос фракционной перестройки отправлен. ГМ разрешит переход или нейтральный уход.",
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyShiningFactionLeadershipAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_shining_politics_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите запрос смены главы.");

        await _stateManager.RefreshGameStateAsync();
        var realmBlocker = await TryBuildShiningPoliticsRealmBlockerAsync("Смена главы сияющей фракции недоступна");
        if (realmBlocker != null)
            return realmBlocker;

        var factionId = ReadAnswer(answers, "faction_id");
        var mode = ReadAnswer(answers, "transition_mode").Trim().ToLowerInvariant();
        var candidateChoice = ReadAnswer(answers, "candidate_head_choice");
        var supporterIds = ParseIdList(ReadAnswer(answers, "supporting_resident_ids"));

        if (string.IsNullOrWhiteSpace(factionId))
            return BrowserPromptWriteResult.ValidationError("Выберите фракцию.");
        if (!ShiningFactionRequestState.IsSupportedTransitionMode(mode))
            return BrowserPromptWriteResult.ValidationError("Выберите поддерживаемый режим смены власти.");
        if (!string.Equals(mode, ShiningFactionRequestState.TransitionModeAbdication, StringComparison.OrdinalIgnoreCase) &&
            !TryParseActorChoice(candidateChoice, out _, out _))
        {
            return BrowserPromptWriteResult.ValidationError("Выберите кандидата на главу.");
        }

        return await ExecuteAsync(
            owner,
            "Смена главы сияющей фракции",
            [ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, GuardianAbodeResidentState.StatePath, ShiningAbodeState.StatePath, GuardiansPath],
            async () =>
            {
                var shiningRoot = await ReadRequiredObjectAsync(ShiningAbodeState.StatePath, "Состояние Сияющей Обители сейчас недоступно.");
                var residentsRoot = await ReadRequiredObjectAsync(GuardianAbodeResidentState.StatePath, "Состав обитателей сейчас недоступен.");
                var guardiansRoot = await TryReadObjectSafeAsync(GuardiansPath);
                var faction = FindVisibleOperationalFaction(shiningRoot, factionId);
                if (faction == null)
                    throw new InvalidOperationException("Выберите видимую действующую фракцию Сияющей Обители.");

                var leadership = faction["leadership"] as JsonObject ?? new JsonObject();
                TryParseActorChoice(candidateChoice, out var candidateType, out var candidateId);
                if (string.Equals(mode, ShiningFactionRequestState.TransitionModeAbdication, StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(candidateChoice))
                {
                    candidateType = string.Empty;
                    candidateId = string.Empty;
                }

                if (!string.Equals(mode, ShiningFactionRequestState.TransitionModeAbdication, StringComparison.OrdinalIgnoreCase) &&
                    !IsVisibleLeadershipCandidate(shiningRoot, residentsRoot, guardiansRoot, factionId, candidateType, candidateId))
                {
                    throw new InvalidOperationException("Выберите видимого кандидата, подходящего для этой фракции.");
                }

                if (supporterIds.Any(supporterId => !IsVisibleAscendedResidentForPolitics(shiningRoot, residentsRoot, supporterId, allowFactionless: false, requiredFactionId: factionId)))
                    throw new InvalidOperationException("Выберите видимых вознесённых сторонников из этой фракции.");

                var request = new ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest
                {
                    FactionId = factionId.Trim(),
                    FactionName = ResolveFactionName(faction, factionId.Trim()),
                    TransitionMode = mode,
                    IncumbentHeadActorType = GetNodeString(leadership["headActorType"]) ?? string.Empty,
                    IncumbentHeadActorId = GetNodeString(leadership["headActorId"]) ?? string.Empty,
                    CandidateHeadActorType = candidateType,
                    CandidateHeadActorId = candidateId,
                    SupportingResidentIds = supporterIds,
                    CreatedAtTurn = Math.Max(1, _stateManager.CurrentState.TurnNumber + 1)
                };

                var validation = await ShiningFactionRequestState.ValidateLeadershipTransitionRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(validation))
                    throw new InvalidOperationException(SanitizeShiningPoliticsValidationMessage(validation));

                await ShiningFactionRequestState.WriteLeadershipTransitionRequestAsync(_fs, request);
                await _stateManager.RefreshGameStateAsync();
            },
            "Смена главы отправлена ГМ",
            "Запрос смены главы сияющей фракции отправлен. ГМ разрешит политический исход.",
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyShiningNativeFactionDiscoveryAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_shining_core_action_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите открытие нативной фракции.");

        await _stateManager.RefreshGameStateAsync();
        var realmBlocker = await TryBuildShiningCoreActionRealmBlockerAsync("Открытие нативной фракции недоступно");
        if (realmBlocker != null)
            return realmBlocker;

        return await ExecuteAsync(
            owner,
            "Открытие нативной фракции",
            [ShiningCoreActionRequestState.PendingActionsRequestPath, ShiningAbodeState.StatePath, SoulStatePath],
            async () =>
            {
                var shiningRoot = await ReadRequiredObjectAsync(ShiningAbodeState.StatePath, "Состояние Сияющей Обители сейчас недоступно.");
                var cost = ShiningAbodeState.GetNativeDiscoveryCost();
                var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    ActionType = ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction,
                    RadianceTierAtRequest = GetNodeInt(shiningRoot["radiance"]?["tier"]),
                    QuotedCostFeathers = cost.Feathers,
                    QuotedCostLightSparks = cost.LightSparks,
                    CreatedAtTurn = Math.Max(1, _stateManager.CurrentState.TurnNumber + 1)
                };

                var validation = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(validation))
                    throw new InvalidOperationException(SanitizeShiningCoreActionValidationMessage(validation));

                await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
                await _stateManager.RefreshGameStateAsync();
            },
            "Открытие отправлено ГМ",
            "Запрос открытия нативной фракции отправлен. ГМ разрешит появление новой сияющей фракции.",
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyShiningFactionInvestmentAsync(
        string commandArguments,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_shining_core_action_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите инвестицию в сияющую фракцию.");

        var factionId = string.IsNullOrWhiteSpace(commandArguments)
            ? ReadAnswer(answers, "faction_id")
            : commandArguments.Trim();
        if (string.IsNullOrWhiteSpace(factionId))
            return BrowserPromptWriteResult.ValidationError("Выберите сияющую фракцию.");

        await _stateManager.RefreshGameStateAsync();
        var realmBlocker = await TryBuildShiningCoreActionRealmBlockerAsync("Инвестиция в сияющую фракцию недоступна");
        if (realmBlocker != null)
            return realmBlocker;

        return await ExecuteAsync(
            owner,
            "Инвестиция в сияющую фракцию",
            [ShiningCoreActionRequestState.PendingActionsRequestPath, ShiningAbodeState.StatePath, GuardianAbodeResidentState.StatePath, SoulStatePath],
            async () =>
            {
                var shiningRoot = await ReadRequiredObjectAsync(ShiningAbodeState.StatePath, "Состояние Сияющей Обители сейчас недоступно.");
                var faction = FindVisibleOperationalFaction(shiningRoot, factionId);
                if (faction == null)
                    throw new InvalidOperationException("Выберите видимую действующую фракцию Сияющей Обители.");

                var cost = ShiningAbodeState.GetFactionInvestmentCost();
                var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    ActionType = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
                    FactionId = factionId.Trim(),
                    FactionName = ResolveFactionName(faction, factionId.Trim()),
                    QuotedCostFeathers = cost.Feathers,
                    QuotedCostLightSparks = cost.LightSparks,
                    CreatedAtTurn = Math.Max(1, _stateManager.CurrentState.TurnNumber + 1)
                };

                var validation = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(validation))
                    throw new InvalidOperationException(SanitizeShiningCoreActionValidationMessage(validation));

                await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
                await _stateManager.RefreshGameStateAsync();
            },
            "Инвестиция отправлена ГМ",
            "Запрос инвестиции в сияющую фракцию отправлен. ГМ разрешит итог вложения.",
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyShiningProjectSupportMutationAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner,
        bool support)
    {
        if (!ReadBoolAnswer(answers, "confirm_shining_core_action_write"))
            return BrowserPromptWriteResult.ValidationError(support ? "Подтвердите поддержку проекта." : "Подтвердите снятие поддержки проекта.");

        var projectChoice = ReadAnswer(answers, "project_choice");
        if (!TryParseProjectChoice(projectChoice, out var factionId, out var projectId))
            return BrowserPromptWriteResult.ValidationError("Выберите сияющий проект.");

        await _stateManager.RefreshGameStateAsync();
        var realmBlocker = await TryBuildShiningCoreActionRealmBlockerAsync(support ? "Поддержка сияющего проекта недоступна" : "Снятие поддержки сияющего проекта недоступно");
        if (realmBlocker != null)
            return realmBlocker;

        return await ExecuteAsync(
            owner,
            support ? "Поддержка сияющего проекта" : "Снятие поддержки сияющего проекта",
            [ShiningCoreActionRequestState.PendingActionsRequestPath, ShiningAbodeState.StatePath],
            async () =>
            {
                var (faction, project) = await ResolveVisibleShiningProjectAsync(factionId, projectId);
                if (!support && !IsCompletedProject(project))
                    throw new InvalidOperationException("Снимать поддержку можно только с завершённого проекта.");

                var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    ActionType = support
                        ? ShiningCoreActionRequestState.ActionTypeSupportProject
                        : ShiningCoreActionRequestState.ActionTypeUnsupportProject,
                    FactionId = factionId,
                    FactionName = ResolveFactionName(faction, factionId),
                    ProjectId = projectId,
                    ProjectDisplayName = ResolveProjectName(project, projectId),
                    CreatedAtTurn = Math.Max(1, _stateManager.CurrentState.TurnNumber + 1)
                };

                var validation = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(validation))
                    throw new InvalidOperationException(SanitizeShiningCoreActionValidationMessage(validation));

                await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
                await _stateManager.RefreshGameStateAsync();
            },
            support ? "Поддержка отправлена ГМ" : "Снятие поддержки отправлено ГМ",
            support
                ? "Запрос поддержки сияющего проекта отправлен. ГМ разрешит итог поддержки."
                : "Запрос снятия поддержки сияющего проекта отправлен. ГМ разрешит итог изменения.",
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyShiningProjectRetirementAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_shining_core_action_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите отправку проекта в историю.");

        var projectChoice = ReadAnswer(answers, "project_choice");
        if (!TryParseProjectChoice(projectChoice, out var factionId, out var projectId))
            return BrowserPromptWriteResult.ValidationError("Выберите сияющий проект.");

        await _stateManager.RefreshGameStateAsync();
        var realmBlocker = await TryBuildShiningCoreActionRealmBlockerAsync("Отправка сияющего проекта в историю недоступна");
        if (realmBlocker != null)
            return realmBlocker;

        return await ExecuteAsync(
            owner,
            "Отправка сияющего проекта в историю",
            [ShiningCoreActionRequestState.PendingActionsRequestPath, ShiningAbodeState.StatePath, GuardianAbodeResidentState.StatePath],
            async () =>
            {
                var (faction, project) = await ResolveVisibleShiningProjectAsync(factionId, projectId);
                var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    ActionType = ShiningCoreActionRequestState.ActionTypeRetireProject,
                    FactionId = factionId,
                    FactionName = ResolveFactionName(faction, factionId),
                    ProjectId = projectId,
                    ProjectDisplayName = ResolveProjectName(project, projectId),
                    CreatedAtTurn = Math.Max(1, _stateManager.CurrentState.TurnNumber + 1)
                };

                var validation = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(validation))
                    throw new InvalidOperationException(SanitizeShiningCoreActionValidationMessage(validation));

                await ShiningCoreActionRequestState.WriteRequestAsync(_fs, request);
                await _stateManager.RefreshGameStateAsync();
            },
            "Проект отправлен ГМ",
            "Запрос отправки сияющего проекта в историю передан ГМ.",
            payload: null);
    }

    private async Task<BrowserPromptWriteResult?> TryBuildShiningPoliticsRealmBlockerAsync(string title)
    {
        JsonObject? soulRoot = null;
        JsonObject? shiningRoot = null;
        try
        {
            soulRoot = await ReadObjectAsync(SoulStatePath);
            shiningRoot = await ReadObjectAsync(ShiningAbodeState.StatePath);
        }
        catch
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Blocked,
                UiNotificationSeverity.Warning,
                title,
                "Политическое действие временно недоступно: состояние души или Сияющей Обители нужно восстановить перед запросом.");
        }

        var currentRealm = FirstNonEmpty(GetNodeString(soulRoot?["currentRealm"]), _stateManager.CurrentState.CurrentRealm);
        if (!RealmSemantics.IsShiningRealm(currentRealm))
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Blocked,
                UiNotificationSeverity.Warning,
                title,
                "Политические действия доступны только в Сияющей Обители. Сейчас действие недоступно для текущего царства.");
        }

        if (shiningRoot == null ||
            !string.Equals(GetNodeString(shiningRoot["availability"]), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase) ||
            ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot) != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Blocked,
                UiNotificationSeverity.Warning,
                title,
                "Политические действия доступны только в обычной активной Сияющей Обители.");
        }

        var rawStateError = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot);
        if (!string.IsNullOrWhiteSpace(rawStateError))
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Blocked,
                UiNotificationSeverity.Warning,
                title,
                "Сияющая Обитель сейчас не готова к политическим действиям. Проверьте состояние перед новым запросом.");
        }

        return null;
    }

    private async Task<BrowserPromptWriteResult?> TryBuildShiningCoreActionRealmBlockerAsync(string title)
    {
        JsonObject? soulRoot = null;
        JsonObject? shiningRoot = null;
        try
        {
            soulRoot = await ReadObjectAsync(SoulStatePath);
            shiningRoot = await ReadObjectAsync(ShiningAbodeState.StatePath);
        }
        catch
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Blocked,
                UiNotificationSeverity.Warning,
                title,
                "Действие Сияющей Обители временно недоступно: состояние души или Обители нужно восстановить перед запросом.");
        }

        var currentRealm = FirstNonEmpty(GetNodeString(soulRoot?["currentRealm"]), _stateManager.CurrentState.CurrentRealm);
        if (!RealmSemantics.IsShiningRealm(currentRealm))
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Blocked,
                UiNotificationSeverity.Warning,
                title,
                "Действия Сияющей Обители доступны только в Сияющей Обители. Сейчас действие недоступно для текущего царства.");
        }

        if (shiningRoot == null ||
            !string.Equals(GetNodeString(shiningRoot["availability"]), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase) ||
            ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot) != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Blocked,
                UiNotificationSeverity.Warning,
                title,
                "Действия доступны только в обычной активной Сияющей Обители.");
        }

        var rawStateError = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot);
        if (!string.IsNullOrWhiteSpace(rawStateError))
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Blocked,
                UiNotificationSeverity.Warning,
                title,
                "Сияющая Обитель сейчас не готова к действиям. Проверьте состояние перед новым запросом.");
        }

        return null;
    }

    private async Task<(JsonObject Faction, JsonObject Project)> ResolveVisibleShiningProjectAsync(
        string factionId,
        string projectId)
    {
        var shiningRoot = await ReadRequiredObjectAsync(ShiningAbodeState.StatePath, "Состояние Сияющей Обители сейчас недоступно.");
        var faction = FindVisibleOperationalFaction(shiningRoot, factionId);
        if (faction == null)
            throw new InvalidOperationException("Выберите видимую действующую фракцию Сияющей Обители.");

        var project = FindProject(faction, projectId);
        if (project == null || !IsPlayerVisibleObject(project))
            throw new InvalidOperationException("Выберите видимый проект этой сияющей фракции.");

        return (faction, project);
    }

    private static async Task<GuardianTradeService.GuardianTradeOperationResult> BuildGuardianTradeRequestResultAsync(
        GuardianTradeService service,
        string guardianId,
        int currentIncarnation,
        int currentTurn)
    {
        var view = await service.EnsureTradeInventoryAsync(guardianId, currentIncarnation, currentTurn, createPendingRequests: true);
        if (view == null)
            return new GuardianTradeService.GuardianTradeOperationResult(false, false, "Хранитель не найден.");
        if (view.TradeBlocked)
            return new GuardianTradeService.GuardianTradeOperationResult(false, false, view.BlockReason ?? "Торговля недоступна.");
        if (view.InventoryReady)
            return new GuardianTradeService.GuardianTradeOperationResult(true, false, "Витрина Хранителя уже готова.");
        if (view.InventoryRequestPending)
            return new GuardianTradeService.GuardianTradeOperationResult(true, view.InventoryRequestCreatedThisCall, view.InventoryStatusMessage ?? "Витрина Хранителя запрошена.");
        return new GuardianTradeService.GuardianTradeOperationResult(false, false, view.InventoryStatusMessage ?? "Не удалось запросить витрину Хранителя.");
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

        GachaSoulValidationState validation;
        try
        {
            validation = await ReadGachaSoulValidationStateAsync();
        }
        catch (Exception ex)
        {
            return BrowserPromptWriteResult.ValidationError($"soul_state.json недоступен или повреждён: {ex.Message}");
        }
        if (!RealmSemantics.IsChaosSea(validation.CurrentRealm))
            return BrowserPromptWriteResult.ValidationError(DescribeDirectGachaRealmBlocker(validation.CurrentRealm));
        if (validation.InkFeathers < cost)
            return BrowserPromptWriteResult.ValidationError($"Недостаточно Чернильных Перьев: доступно {validation.InkFeathers}, нужно {cost}.");

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
                var currentRealm = GetNodeString(soulRoot["currentRealm"]);
                if (!RealmSemantics.IsChaosSea(currentRealm))
                    throw new InvalidOperationException(DescribeDirectGachaRealmBlocker(currentRealm));

                var currentFeathers = GetSoulInkFeathers(soulRoot);
                if (currentFeathers < cost)
                    throw new InvalidOperationException($"Недостаточно Чернильных Перьев: доступно {currentFeathers}, нужно {cost}.");

                string? stagedRollbackPath = null;
                try
                {
                    stagedRollbackPath = await ExplorerLocalTurnRollbackArtifacts.StageFileAsync(
                        _fs,
                        SoulStatePath,
                        "browser_direct_gacha");

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
                    var queuedTurn = await _turnRequestQueue.QueueDirectChaosSeaGachaAsync(
                        owner,
                        gmAction,
                        pending,
                        stagedRollbackPath ?? throw new InvalidOperationException("Не удалось сохранить pre-spend rollback evidence для direct /gacha."),
                        currentRealm ?? "Chaos Sea");
                    payload["remainingInkFeathers"] = remainingFeathers;
                    payload["currentInkFeathersBeforeSpend"] = currentFeathers;
                    payload["playerActionTag"] = "CHAOS_SEA_DIRECT_GACHA";
                    payload["gachaBaseResult"] = gachaBase;
                    payload["rarityRule"] = "finalRarity exactly equals gachaBaseResult.baseRarity; no guardian modifiers";
                    payload["expectedRelicMaterialization"] = "GM appends exactly one new Soul Relic; the browser does not materialize a concrete relic locally.";
                    payload["gmAction"] = gmAction;
                    payload["queuedTurnRequest"] = new JsonObject
                    {
                        ["sessionId"] = queuedTurn.SessionId,
                        ["requestId"] = queuedTurn.RequestId,
                        ["turnNumber"] = queuedTurn.TurnNumber,
                        ["playerAction"] = queuedTurn.PlayerAction
                    };
                    payload["affectedFiles"] = new JsonArray(
                        SoulStatePath,
                        PendingTurnStateService.PendingDiceStatePath,
                        BrowserPendingTurnInspector.TurnRequestPath,
                        BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath,
                        PendingTurnSnapshotAuthority.AuthorityPath);
                }
                catch
                {
                    ExplorerLocalTurnRollbackArtifacts.DeleteBackup(_fs, stagedRollbackPath);
                    throw;
                }
            },
            "Прямой призыв подготовлен",
            "Браузер списал Чернильные Перья и поставил ход ГМ в очередь: результатом должна стать ровно одна материализованная Реликвия Души без локального выбора имени.",
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
        JsonObject? payload)
    {
        var result = await _coordinator.ExecuteAsync(
            new BrowserLocalWriteRequest(owner.OwnerId, owner.OwnerLabel, operationLabel),
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

        var sanitized = message
            .Replace("Browser-write отменён, rollback восстановлен:", "Локальная запись не выполнена, состояние восстановлено:", StringComparison.Ordinal)
            .Replace("Browser-write", "Локальная запись", StringComparison.Ordinal)
            .Replace("GM-turn", "ход ГМ", StringComparison.Ordinal)
            .Replace("rollback/snapshot artifact", "восстановление состояния", StringComparison.Ordinal)
            .Replace("rollback", "восстановление состояния", StringComparison.Ordinal)
            .Replace("game_session", "текущая игровая сессия", StringComparison.Ordinal)
            .Replace("lease", "срока блокировки", StringComparison.Ordinal);

        return ContainsBrowserTradeDiagnosticFragment(sanitized)
            ? "Локальная запись не выполнена, состояние восстановлено: действие временно ждёт проверки ГМ. Завершите или обновите текущий запрос, затем повторите действие."
            : sanitized;
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

    private async Task<GachaSoulValidationState> ReadGachaSoulValidationStateAsync()
    {
        var soulRoot = await ReadRequiredObjectAsync(SoulStatePath, "soul_state.json недоступен.");
        return new GachaSoulValidationState(
            GetNodeString(soulRoot["currentRealm"]) ?? string.Empty,
            GetSoulInkFeathers(soulRoot));
    }

    private static string DescribeDirectGachaRealmBlocker(string? currentRealm) =>
        $"Прямой призыв Моря Хаоса доступен только в Ordinary Chaos Sea (currentRealm=Chaos Sea/Море Хаоса). Текущий realm: {FirstNonEmpty(currentRealm, "не определён")}.";

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

    private static List<string> ParseIdList(string value) =>
        value
            .Split([',', ';', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string BuildSlugId(string prefix, string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var builder = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            else if (builder.Length > 0 && builder[^1] != '_')
                builder.Append('_');
        }

        var slug = builder.ToString().Trim('_');
        return $"{prefix}_{slug}";
    }

    private static List<string> BuildFoundingServiceTags(string patronEffectFamily, string secondaryTag)
    {
        var tags = new List<string> { MapPatronFamilyToHallServiceTag(patronEffectFamily) };
        if (!string.IsNullOrWhiteSpace(secondaryTag) &&
            ShiningAbodeState.IsSupportedHallServiceTag(secondaryTag) &&
            !tags.Contains(secondaryTag.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(secondaryTag.Trim());
        }

        return tags;
    }

    private static string MapPatronFamilyToHallServiceTag(string patronEffectFamily) => patronEffectFamily switch
    {
        ShiningAbodeState.EffectFamilyLore => ShiningAbodeState.HallServiceTagLore,
        ShiningAbodeState.EffectFamilyMemory => ShiningAbodeState.HallServiceTagMemory,
        ShiningAbodeState.EffectFamilyResource => ShiningAbodeState.HallServiceTagResource,
        ShiningAbodeState.EffectFamilyRelic => ShiningAbodeState.HallServiceTagRelic,
        ShiningAbodeState.EffectFamilyDescent or ShiningAbodeState.EffectFamilyRoute => ShiningAbodeState.HallServiceTagDescent,
        _ => ShiningAbodeState.HallServiceTagSocial
    };

    private static JsonObject? FindResident(JsonObject? residentsRoot, string residentId)
    {
        if (residentsRoot?[GuardianAbodeResidentState.EntriesProperty] is not JsonArray entries)
            return null;

        return entries.OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(GetNodeString(entry["residentId"]), residentId, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject? FindFaction(JsonObject? shiningRoot, string factionId)
    {
        if (shiningRoot?["factions"] is not JsonArray factions)
            return null;

        return factions.OfType<JsonObject>()
            .FirstOrDefault(faction => string.Equals(GetNodeString(faction["factionId"]), factionId, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject? FindProject(JsonObject? faction, string projectId)
    {
        if (faction?["projects"] is not JsonArray projects)
            return null;

        return projects.OfType<JsonObject>()
            .FirstOrDefault(project => string.Equals(GetNodeString(project["projectId"]), projectId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCompletedProject(JsonObject project) =>
        string.Equals(GetNodeString(project["status"]), ShiningAbodeState.ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase);

    private static JsonObject? FindVisibleOperationalFaction(JsonObject? shiningRoot, string factionId) =>
        shiningRoot == null || string.IsNullOrWhiteSpace(factionId)
            ? null
            : SarefMainStoryState.GetPlayerVisibleShiningFactions(shiningRoot)
                .Where(IsPlayerVisibleObject)
                .Where(static faction => ShiningAbodeState.IsFactionOperational(faction))
                .FirstOrDefault(faction => string.Equals(GetNodeString(faction["factionId"]), factionId, StringComparison.OrdinalIgnoreCase));

    private static bool IsVisibleAscendedResidentForPolitics(
        JsonObject shiningRoot,
        JsonObject? residentsRoot,
        string residentId,
        bool allowFactionless,
        string? requiredFactionId = null)
    {
        var resident = FindResident(residentsRoot, residentId);
        if (resident == null ||
            !string.Equals(GetNodeString(resident["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var factionId = GetNodeString(resident["shiningFactionId"]) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(requiredFactionId) &&
            !string.Equals(factionId, requiredFactionId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(factionId))
            return allowFactionless;

        return FindVisibleOperationalFaction(shiningRoot, factionId) != null;
    }

    private static bool IsVisibleLeadershipCandidate(
        JsonObject shiningRoot,
        JsonObject? residentsRoot,
        JsonObject? guardiansRoot,
        string factionId,
        string candidateType,
        string candidateId)
    {
        if (string.Equals(candidateType, ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase))
            return string.Equals(candidateId, ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase);

        if (string.Equals(candidateType, ShiningAbodeState.HeadActorTypeResident, StringComparison.OrdinalIgnoreCase))
            return IsVisibleAscendedResidentForPolitics(shiningRoot, residentsRoot, candidateId, allowFactionless: false, requiredFactionId: factionId);

        if (string.Equals(candidateType, ShiningAbodeState.HeadActorTypeGuardian, StringComparison.OrdinalIgnoreCase))
            return GuardianExists(guardiansRoot, candidateId);

        if (!string.Equals(candidateType, ShiningAbodeState.HeadActorTypeRadiantActor, StringComparison.OrdinalIgnoreCase))
            return false;

        var actor = FindVisibleRadiantActor(shiningRoot, candidateId);
        if (actor == null)
            return false;

        var currentFactionId = GetNodeString(actor["currentFactionId"]) ?? string.Empty;
        return string.IsNullOrWhiteSpace(currentFactionId) ||
               string.Equals(currentFactionId, factionId, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject? FindVisibleRadiantActor(JsonObject shiningRoot, string actorId)
    {
        if (shiningRoot["shiningPoliticalActors"] is not JsonArray actors)
            return null;

        return actors.OfType<JsonObject>()
            .Where(IsPlayerVisibleObject)
            .FirstOrDefault(actor => string.Equals(GetNodeString(actor["actorId"]), actorId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool GuardianExists(JsonObject? guardiansRoot, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(guardianId))
            return false;

        if (guardiansRoot?["activeGuardian"] is JsonObject activeGuardian &&
            (string.Equals(GetNodeString(activeGuardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(GetNodeString(activeGuardian["id"]), guardianId, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return guardiansRoot?["guardians"] is JsonArray guardians &&
               guardians.OfType<JsonObject>().Any(guardian =>
                   string.Equals(GetNodeString(guardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(GetNodeString(guardian["id"]), guardianId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPlayerVisibleObject(JsonObject entry)
    {
        if (IsFalseFlag(entry["isPlayerVisible"]) || IsFalseFlag(entry["playerVisible"]))
            return false;

        return !IsHiddenText(GetNodeString(entry["visibility"]));
    }

    private static bool IsFalseFlag(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<bool>(out var flag) &&
        !flag;

    private static bool IsHiddenText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        return string.Equals(normalized, "hidden", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "gm_only", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "secret", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "private", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "internal", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "faction-internal", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveFactionName(JsonObject? faction, string fallbackId) =>
        FirstNonEmpty(
            GetNodeString(faction?["charter"]?["factionName"]),
            GetNodeString(faction?["factionName"]),
            fallbackId);

    private static string ResolveProjectName(JsonObject? project, string fallbackId) =>
        FirstNonEmpty(
            GetNodeString(project?["displayName"]),
            GetNodeString(project?["projectName"]),
            fallbackId);

    private static string GetResidentName(JsonObject resident) =>
        FirstNonEmpty(
            GetNodeString(resident["displayName"]),
            GetNodeString(resident["residentName"]),
            GetNodeString(resident["residentId"]));

    private static bool TryParseActorChoice(string value, out string actorType, out string actorId)
    {
        actorType = string.Empty;
        actorId = string.Empty;
        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !ShiningAbodeState.IsSupportedHeadActorType(parts[0]) ||
            string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        actorType = parts[0];
        actorId = parts[1];
        return true;
    }

    private static bool TryParseProjectChoice(string value, out string factionId, out string projectId)
    {
        factionId = string.Empty;
        projectId = string.Empty;
        var parts = value.Split('|', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            return false;

        factionId = parts[0];
        projectId = parts[1];
        return true;
    }

    private static string SanitizeShiningPoliticsValidationMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Политический запрос не прошёл проверку состояния.";

        if (message.Contains("currentRealm", StringComparison.OrdinalIgnoreCase))
            return "Политические действия доступны только в Сияющей Обители.";

        return ContainsBrowserTradeDiagnosticFragment(message)
            ? "Политический запрос временно ждёт проверки состояния. Повторите действие после восстановления политических ожиданий."
            : message;
    }

    private static string SanitizeShiningCoreActionValidationMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Действие Сияющей Обители не прошло проверку состояния.";

        if (message.Contains("currentRealm", StringComparison.OrdinalIgnoreCase))
            return "Действия Сияющей Обители доступны только в Сияющей Обители.";

        return "Действие Сияющей Обители временно ждёт проверки состояния. Повторите действие после восстановления текущих ожиданий.";
    }

    private async Task<JsonObject?> ReadObjectAsync(string path)
    {
        var raw = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return JsonNode.Parse(raw) as JsonObject;
    }

    private async Task<JsonObject?> TryReadObjectSafeAsync(string path)
    {
        try
        {
            return await ReadObjectAsync(path);
        }
        catch
        {
            return null;
        }
    }

    private async Task<JsonObject> ReadRequiredObjectAsync(string path, string error)
    {
        var root = await ReadObjectAsync(path);
        return root ?? throw new InvalidOperationException(error);
    }

    private async Task WriteObjectAsync(string path, JsonObject root) =>
        await _fs.WriteFileAtomicAsync(path, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

    private static CommandParts ParseCommand(string command)
    {
        var split = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return split.Length switch
        {
            0 => new CommandParts(string.Empty, string.Empty),
            1 => new CommandParts(split[0].ToLowerInvariant(), string.Empty),
            _ => new CommandParts(split[0].ToLowerInvariant(), split[1].Trim())
        };
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

    private static JsonObject? FindGuardianByIdOrName(JsonNode node, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
            return null;

        return EnumerateCanonicalGuardianObjects(node)
            .FirstOrDefault(guardian => GuardianMatches(guardian, expected));
    }

    private static IEnumerable<GuardianAbodeBrowserOption> CollectGuardianAbodeOptions(JsonNode node)
    {
        return EnumerateCanonicalGuardianObjects(node)
            .Select(static guardian =>
            {
                var stableId = FirstNonEmpty(GetNodeString(guardian["guardianId"]), GetNodeString(guardian["id"]));
                if (string.IsNullOrWhiteSpace(stableId) || guardian["abode"] is not JsonObject abode)
                    return null;

                var abodeId = FirstNonEmpty(GetNodeString(abode["abodeId"]), GetNodeString(abode["id"]));
                if (string.IsNullOrWhiteSpace(abodeId))
                    return null;

                var manifestation = guardian["manifestation"] as JsonObject;
                var guardianName = FirstNonEmpty(
                    GetNodeString(guardian["canonicalName"]),
                    GetNodeString(guardian["guardianName"]),
                    GetNodeString(guardian["name"]),
                    GetNodeString(guardian["displayName"]),
                    GetNodeString(manifestation?["currentDisplayName"]),
                    stableId);
                var relationship = guardian["relationshipData"] as JsonObject;
                return new GuardianAbodeBrowserOption(
                    stableId,
                    guardianName,
                    FirstNonEmpty(GetNodeString(guardian["domain"]), GetNodeString(guardian["afterlifeDomain"])),
                    abodeId,
                    FirstNonEmpty(GetNodeString(abode["name"]), GetNodeString(abode["displayName"]), GetNodeString(guardian["abodeName"]), abodeId),
                    GetNodeInt(relationship?["currentReputation"], GetNodeInt(guardian["reputation"])),
                    AbodePowerRules.GetCurrentPower(guardian),
                    guardian);
            })
            .Where(static option => option != null)
            .Cast<GuardianAbodeBrowserOption>()
            .DistinctBy(static option => option.CompositeId, StringComparer.OrdinalIgnoreCase);
    }

    private static GuardianAbodeBrowserOption? ResolveGuardianAbodeOption(
        IReadOnlyList<GuardianAbodeBrowserOption> abodes,
        string requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return null;

        return abodes.FirstOrDefault(abode =>
                   string.Equals(abode.CompositeId, requested, StringComparison.OrdinalIgnoreCase)) ??
               abodes.FirstOrDefault(abode =>
                   string.Equals(abode.GuardianId, requested, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(abode.AbodeId, requested, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(abode.GuardianName, requested, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(abode.AbodeName, requested, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<ResidentWriteContext> ReadResidentWriteContextAsync()
    {
        var guardiansRoot = await TryReadObjectSafeAsync("game_state/meta/guardians.json");
        if (guardiansRoot == null)
            return ResidentWriteContext.Failed("Список Хранителей сейчас недоступен.");

        var residentsRoot = await TryReadObjectSafeAsync(GuardianAbodeResidentState.StatePath);
        if (residentsRoot == null)
            return new ResidentWriteContext(guardiansRoot, null, []);

        if (residentsRoot["entries"] is not JsonArray entries)
            return new ResidentWriteContext(guardiansRoot, residentsRoot, []);

        var abodes = CollectGuardianAbodeOptions(guardiansRoot).ToList();
        var powerByGuardian = GuardianAbodeResidentState.CollectGuardianAbodePowerById(guardiansRoot);
        var residents = new List<ResidentWriteOption>();
        foreach (var entry in entries.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(entry["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId))
                continue;

            var currentPower = powerByGuardian.TryGetValue(guardianId, out var power) ? power : (int?)null;
            var resident = GuardianAbodeResidentState.ReadResidentEntry(entry, currentPower);
            if (string.IsNullOrWhiteSpace(resident.ResidentId))
                continue;

            var abode = abodes.FirstOrDefault(candidate =>
                string.Equals(candidate.GuardianId, resident.GuardianId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.AbodeId, resident.AbodeId, StringComparison.OrdinalIgnoreCase));
            residents.Add(new ResidentWriteOption(
                resident,
                FirstNonEmpty(abode?.GuardianName, resident.GuardianId),
                FirstNonEmpty(abode?.AbodeName, resident.AbodeId)));
        }

        return new ResidentWriteContext(
            guardiansRoot,
            residentsRoot,
            residents
                .DistinctBy(static resident => resident.Entry.ResidentId, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static ResidentWriteOption? ResolveResidentWriteOption(
        IReadOnlyList<ResidentWriteOption> residents,
        string requestedResident)
    {
        if (string.IsNullOrWhiteSpace(requestedResident))
            return null;

        return residents.FirstOrDefault(resident =>
                   string.Equals(resident.Entry.ResidentId, requestedResident, StringComparison.OrdinalIgnoreCase)) ??
               residents.FirstOrDefault(resident =>
                   string.Equals(resident.Entry.DisplayName, requestedResident, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ResidentInteractionAllowed(GuardianAbodeResidentState.ResidentEntry resident, string interactionType)
    {
        var allowed = resident.AvailableInteractions
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return allowed.Count == 0 || allowed.Contains(interactionType);
    }

    private static bool TryBuildTransferChoice(
        string transferChoice,
        ResidentWriteOption resident,
        JsonObject guardiansRoot,
        JsonObject? residentsRoot,
        out GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest transferRequest,
        out string validationMessage)
    {
        transferRequest = new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest
        {
            ResidentId = resident.Entry.ResidentId,
            ResidentName = resident.Entry.DisplayName,
            SourceGuardianId = resident.Entry.GuardianId,
            SourceGuardianName = resident.GuardianName,
            SourceAbodeId = resident.Entry.AbodeId,
            SourceAbodeName = resident.AbodeName,
            AbodeDevotionLevel = resident.Entry.AbodeDevotionLevel,
            AbodeDevotionTier = resident.Entry.AbodeDevotionTier,
            Restlessness = resident.Entry.Restlessness,
            MigrationState = resident.Entry.MigrationState
        };
        validationMessage = string.Empty;

        if (string.Equals(transferChoice, "departure_only", StringComparison.OrdinalIgnoreCase))
        {
            transferRequest.TransferMode = GuardianAbodeResidentState.TransferModeDepartureOnly;
            transferRequest.SelectionMode = GuardianAbodeResidentRequestState.TransferSelectionModeDepartureOnly;
            return true;
        }

        const string targetPrefix = "target:";
        if (!transferChoice.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            validationMessage = "Выберите доступное направление перехода.";
            return false;
        }

        var target = transferChoice[targetPrefix.Length..];
        var parts = target.Split("::", 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            validationMessage = "Выберите доступную Обитель для перехода.";
            return false;
        }

        var candidate = GuardianAbodeResidentState.BuildTransferCompetitionCandidates(resident.Entry, guardiansRoot, residentsRoot)
            .FirstOrDefault(item =>
                string.Equals(item.TargetGuardianId, parts[0], StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.TargetAbodeId, parts[1], StringComparison.OrdinalIgnoreCase));
        if (candidate == null)
        {
            validationMessage = "Выбранная Обитель сейчас недоступна для перехода.";
            return false;
        }

        transferRequest.TransferMode = GuardianAbodeResidentState.TransferModeAcceptedTransfer;
        transferRequest.TargetGuardianId = candidate.TargetGuardianId;
        transferRequest.TargetGuardianName = candidate.TargetGuardianName;
        transferRequest.TargetAbodeId = candidate.TargetAbodeId;
        transferRequest.TargetAbodeName = candidate.TargetAbodeName;
        transferRequest.SelectionMode = candidate.CompetitionScore >= 50
            ? GuardianAbodeResidentRequestState.TransferSelectionModeCompetitionRecommended
            : GuardianAbodeResidentRequestState.TransferSelectionModeManualOverride;
        transferRequest.CompetitionScore = candidate.CompetitionScore;
        transferRequest.CompetitionLabel = candidate.CompetitionLabel;
        transferRequest.CompetitionReason = candidate.CompetitionReason;
        return true;
    }

    private static IEnumerable<JsonObject> EnumerateCanonicalGuardianObjects(JsonNode node)
    {
        if (node is JsonArray directArray)
        {
            foreach (var guardian in directArray.OfType<JsonObject>())
                yield return guardian;
            yield break;
        }

        if (node is not JsonObject root)
            yield break;

        if (root["guardians"] is JsonArray guardians)
        {
            foreach (var guardian in guardians.OfType<JsonObject>())
                yield return guardian;
        }

        if (root["activeGuardian"] is JsonObject activeGuardian)
            yield return activeGuardian;
    }

    private static bool GuardianMatches(JsonObject guardian, string expected)
    {
        var stableId = FirstNonEmpty(GetNodeString(guardian["guardianId"]), GetNodeString(guardian["id"]));
        if (string.IsNullOrWhiteSpace(stableId))
            return false;

        var manifestation = guardian["manifestation"] as JsonObject;
        var candidates = new[]
        {
            stableId,
            GetNodeString(guardian["canonicalName"]),
            GetNodeString(guardian["guardianName"]),
            GetNodeString(guardian["name"]),
            GetNodeString(guardian["displayName"]),
            GetNodeString(manifestation?["currentDisplayName"])
        };

        return candidates.Any(candidate => string.Equals(candidate, expected, StringComparison.OrdinalIgnoreCase));
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

    private sealed record CommandParts(string Token, string Arguments);

    private sealed record GuardianAbodeBrowserOption(
        string GuardianId,
        string GuardianName,
        string Domain,
        string AbodeId,
        string AbodeName,
        int CurrentReputation,
        int CurrentAbodePower,
        JsonObject Guardian)
    {
        public string CompositeId => $"{GuardianId}::{AbodeId}";
    }

    private sealed record ResidentWriteOption(
        GuardianAbodeResidentState.ResidentEntry Entry,
        string GuardianName,
        string AbodeName);

    private sealed record ResidentWriteContext(
        JsonObject GuardiansRoot,
        JsonObject? ResidentsRoot,
        IReadOnlyList<ResidentWriteOption> Residents,
        string ErrorMessage = "")
    {
        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public static ResidentWriteContext Failed(string errorMessage) =>
            new(new JsonObject(), null, [], errorMessage);
    }

    private sealed record GachaSoulValidationState(string CurrentRealm, int InkFeathers);
}
