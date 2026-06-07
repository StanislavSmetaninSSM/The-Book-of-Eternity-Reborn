using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.WebUi;

public sealed record ExplorerPromptSessionSubmitRequest(
    string SessionId,
    Dictionary<string, JsonNode?> Answers,
    string? OwnerId = null);

public sealed record ExplorerPromptSessionCancelRequest(
    string SessionId,
    string? OwnerId = null);

public sealed class ExplorerWebPromptSessionService
{
    public const string SubmitEndpoint = "/api/explorer/prompt-sessions/submit";
    public const string CancelEndpoint = "/api/explorer/prompt-sessions/cancel";

    private static readonly TimeSpan SessionLease = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan LockLease = TimeSpan.FromSeconds(120);

    private readonly FileSystemManager _fs;
    private readonly ConcurrentDictionary<string, PromptSessionSnapshot> _sessions = new(StringComparer.Ordinal);
    private readonly LocalUiSessionLockService _lockService;
    private readonly TimeProvider _timeProvider;
    private readonly BrowserMortalWorldWriteService _mortalWorldWriteService;
    private readonly BrowserAfterlifeWriteService _afterlifeWriteService;
    private readonly BrowserSarefStoryWriteService _sarefStoryWriteService;

    public ExplorerWebPromptSessionService(
        FileSystemManager fs,
        StateManager? stateManager = null,
        LocalUiSessionLockService? lockService = null,
        TimeProvider? timeProvider = null,
        BrowserMortalWorldWriteService? mortalWorldWriteService = null,
        BrowserAfterlifeWriteService? afterlifeWriteService = null,
        BrowserSarefStoryWriteService? sarefStoryWriteService = null)
    {
        _fs = fs;
        _lockService = lockService ?? new LocalUiSessionLockService(fs, timeProvider);
        _timeProvider = timeProvider ?? TimeProvider.System;
        var coordinator = new BrowserLocalWriteCoordinator(fs, _lockService, _timeProvider);
        var effectiveStateManager = stateManager ?? new StateManager(fs, new Configuration.GameSettings(), NullLogger<StateManager>.Instance);
        _mortalWorldWriteService = mortalWorldWriteService ?? new BrowserMortalWorldWriteService(
            fs,
            coordinator,
            new ScenarioCoreService(fs, NullLogger<ScenarioCoreService>.Instance),
            _timeProvider);
        _afterlifeWriteService = afterlifeWriteService ?? new BrowserAfterlifeWriteService(
            fs,
            effectiveStateManager,
            coordinator);
        _sarefStoryWriteService = sarefStoryWriteService ?? new BrowserSarefStoryWriteService(
            fs,
            effectiveStateManager,
            coordinator);
    }

    public async Task<ExplorerCommandResult> AttachSessionIfNeededAsync(
        ExplorerCommandResult result,
        ExplorerWebCommandRequest request)
    {
        if (result.State != CommandExecutionState.RequiresInput || result.Prompts.Count == 0)
            return result;

        var owner = BuildOwner(request.OwnerId, request.OwnerLabel);
        var requiresLock = RequiresLocalUiLock(result.Command);
        if (requiresLock)
        {
            var pending = BrowserPendingTurnInspector.Build(_fs);
            if (pending.HasActiveGmTurn)
            {
                return new ExplorerCommandResult
                {
                    Command = result.Command,
                    State = CommandExecutionState.Pending,
                    Blocks =
                    [
                        new UiMessageBlock
                        {
                            Severity = UiNotificationSeverity.Warning,
                            Title = "Активный ход ГМа",
                            Message = "Книга занята текущим ходом. Завершите или отмените его, затем повторите действие."
                        }
                    ],
                    Notifications =
                    [
                        new UiNotification
                        {
                            Severity = UiNotificationSeverity.Warning,
                            Title = "Форма не открыта",
                            Message = "Книга занята текущим ходом. Вернитесь к форме после завершения или отмены хода."
                        }
                    ]
                };
            }

            var lockResult = await _lockService.AcquireOrRefreshAsync(owner, "Игровая форма действия");
            if (!lockResult.Acquired)
            {
                return new ExplorerCommandResult
                {
                    Command = result.Command,
                    State = CommandExecutionState.Blocked,
                    Blocks =
                    [
                        new UiMessageBlock
                        {
                            Severity = UiNotificationSeverity.Warning,
                            Title = "Форма уже открыта",
                            Message = "Другая вкладка или окно сейчас записывает действие. Дождитесь завершения и повторите попытку."
                        }
                    ],
                    Notifications =
                    [
                        new UiNotification
                        {
                            Severity = UiNotificationSeverity.Warning,
                            Title = "Форма не открыта",
                            Message = "Другая вкладка или окно сейчас записывает действие."
                        }
                    ]
                };
            }
        }

        var expiresAtUtc = _timeProvider.GetUtcNow().UtcDateTime.Add(SessionLease);
        var session = BuildSession(owner, requiresLock, expiresAtUtc);
        _sessions[session.SessionId] = new PromptSessionSnapshot(
            session,
            result,
            owner,
            requiresLock,
            expiresAtUtc);

        return WithSession(result, session, result.State, result.Prompts);
    }

    public ExplorerCommandResult GetSession(string sessionId)
    {
        if (!TryGetLiveSnapshot(sessionId, out var snapshot))
            return MissingSessionResult(sessionId);

        return WithSession(snapshot.Result, snapshot.Session, CommandExecutionState.RequiresInput, snapshot.Result.Prompts);
    }

    public async Task<ExplorerCommandResult> SubmitAsync(ExplorerPromptSessionSubmitRequest request)
    {
        if (!TryGetLiveSnapshot(request.SessionId, out var snapshot))
            return MissingSessionResult(request.SessionId);

        if (!OwnerMatches(snapshot, request.OwnerId))
            return OwnerMismatchResult(snapshot);

        var answers = request.Answers ?? new Dictionary<string, JsonNode?>();
        var errors = ValidateAnswers(snapshot.Result.Prompts, answers);
        if (errors.Count > 0)
        {
            return WithSession(
                snapshot.Result,
                snapshot.Session,
                CommandExecutionState.RequiresInput,
                snapshot.Result.Prompts,
                errors.Select(error => new UiNotification
                {
                    Severity = UiNotificationSeverity.Error,
                    Title = "Ошибка формы",
                    Message = error
                }).ToList());
        }

        var writeResult = await _mortalWorldWriteService.TryApplyAsync(
            snapshot.Result.Command,
            answers,
            snapshot.Owner);
        if (writeResult.Handled)
            return await BuildDomainWriteSubmitResultAsync(request.SessionId, snapshot, writeResult);

        writeResult = await _afterlifeWriteService.TryApplyAsync(
            snapshot.Result.Command,
            answers,
            snapshot.Owner);
        if (writeResult.Handled)
            return await BuildDomainWriteSubmitResultAsync(request.SessionId, snapshot, writeResult);

        writeResult = await _sarefStoryWriteService.TryApplyAsync(
            snapshot.Result.Command,
            answers,
            snapshot.Owner);
        if (writeResult.Handled)
            return await BuildDomainWriteSubmitResultAsync(request.SessionId, snapshot, writeResult);

        var submittedAnswers = AnswersToJson(answers);
        _sessions.TryRemove(request.SessionId, out _);
        if (snapshot.RequiresLocalUiLock)
            await _lockService.ReleaseAsync(snapshot.Owner);

        var blocks = snapshot.Result.Blocks.ToList();
        blocks.Add(new UiMessageBlock
        {
            Severity = UiNotificationSeverity.Success,
            Title = "Ответы формы приняты",
            Message = "Форма получила ответы. Для этого действия запись пока не подключена, поэтому состояние игры не изменилось."
        });
        blocks.Add(new UiRawJsonBlock
        {
            Title = "Отправленные ответы формы",
            Json = submittedAnswers
        });

        return new ExplorerCommandResult
        {
            Command = snapshot.Result.Command,
            State = CommandExecutionState.Completed,
            Blocks = blocks,
            Actions = snapshot.Result.Actions,
            Notifications =
            [
                new UiNotification
                {
                    Severity = UiNotificationSeverity.Success,
                    Title = "Форма завершена",
                    Message = "Форма получила ответы и закрыта."
                }
            ]
        };
    }

    private async Task<ExplorerCommandResult> BuildDomainWriteSubmitResultAsync(
        string sessionId,
        PromptSessionSnapshot snapshot,
        BrowserPromptWriteResult writeResult)
    {
        if (writeResult.KeepSessionOpen)
        {
            return WithSession(
                snapshot.Result,
                snapshot.Session,
                CommandExecutionState.RequiresInput,
                snapshot.Result.Prompts,
                [
                    new UiNotification
                    {
                        Severity = writeResult.Severity,
                        Title = writeResult.Title,
                        Message = writeResult.Message
                    }
                ]);
        }

        _sessions.TryRemove(sessionId, out _);
        if (!writeResult.Success && snapshot.RequiresLocalUiLock)
            await _lockService.ReleaseAsync(snapshot.Owner);

        var blocks = snapshot.Result.Blocks.ToList();
        blocks.Add(new UiMessageBlock
        {
            Severity = writeResult.Severity,
            Title = writeResult.Title,
            Message = writeResult.Message
        });
        if (writeResult.Payload != null)
        {
            blocks.Add(new UiRawJsonBlock
            {
                Title = "Подробности записи",
                Json = writeResult.Payload.DeepClone()
            });
        }

        return new ExplorerCommandResult
        {
            Command = snapshot.Result.Command,
            State = writeResult.State,
            Blocks = blocks,
            Actions = snapshot.Result.Actions,
            Notifications =
            [
                new UiNotification
                {
                    Severity = writeResult.Severity,
                    Title = writeResult.Title,
                    Message = writeResult.Message
                }
            ]
        };
    }

    public async Task<ExplorerCommandResult> CancelAsync(ExplorerPromptSessionCancelRequest request)
    {
        if (!_sessions.TryRemove(request.SessionId, out var snapshot))
            return MissingSessionResult(request.SessionId);

        if (!OwnerMatches(snapshot, request.OwnerId))
        {
            _sessions[snapshot.Session.SessionId] = snapshot;
            return OwnerMismatchResult(snapshot);
        }

        if (snapshot.RequiresLocalUiLock)
            await _lockService.ReleaseAsync(snapshot.Owner);

        return new ExplorerCommandResult
        {
            Command = snapshot.Result.Command,
            State = CommandExecutionState.Completed,
            Blocks =
            [
                new UiMessageBlock
                {
                    Severity = UiNotificationSeverity.Info,
                    Title = "Форма отменена",
                    Message = "Форма закрыта без изменений."
                }
            ],
            Notifications =
            [
                new UiNotification
                {
                    Severity = UiNotificationSeverity.Info,
                    Title = "Форма отменена",
                    Message = "Форма закрыта; действие можно выбрать заново."
                }
            ]
        };
    }

    private bool TryGetLiveSnapshot(string sessionId, out PromptSessionSnapshot snapshot)
    {
        snapshot = default!;
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var existing))
            return false;

        if (existing.ExpiresAtUtc <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            _sessions.TryRemove(sessionId, out _);
            return false;
        }

        snapshot = existing;
        return true;
    }

    private static UiPromptSession BuildSession(LocalUiSessionLockOwner owner, bool requiresLock, DateTime expiresAtUtc)
    {
        var sessionId = "prompt_" + Guid.NewGuid().ToString("N");
        return new UiPromptSession
        {
            SessionId = sessionId,
            SubmitEndpoint = SubmitEndpoint,
            CancelEndpoint = CancelEndpoint,
            RequiresLocalUiLock = requiresLock,
            OwnerId = owner.OwnerId,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    private static LocalUiSessionLockOwner BuildOwner(string? ownerId, string? ownerLabel)
    {
        var effectiveOwnerId = string.IsNullOrWhiteSpace(ownerId)
            ? $"browser:{Environment.MachineName}:{Environment.ProcessId}"
            : ownerId.Trim();
        var effectiveLabel = string.IsNullOrWhiteSpace(ownerLabel)
            ? $"Local Browser UI PID {Environment.ProcessId}"
            : ownerLabel.Trim();
        return new LocalUiSessionLockOwner(effectiveOwnerId, "browser", effectiveLabel, LockLease);
    }

    private static bool RequiresLocalUiLock(string command)
    {
        var normalized = string.Join(' ', command.Trim().Replace('-', '_').Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        if (normalized is "/сареф найти_крылья" or "/saref find_wings")
            return true;

        var token = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return token is
            "/validate" or "/валидация" or
            "/world_setup" or "/настройка_мира" or
            "/distribute" or "/распределить" or
            "/companion_directive" or "/директива_компаньону" or
            "/faction_directive" or "/директива_фракции" or
            "/npc_talk" or "/talk_npc" or "/поговорить_с_нпс" or "/разговор_с_нпс" or
            "/equip" or "/экипировать" or
            "/unequip" or "/снять" or
            "/inventory_drop" or "/выбросить_предмет" or
            "/inventory_split" or "/разделить_стопку" or
            "/inventory_merge" or "/объединить_стопки" or
            "/npc_trade" or "/торговля_нпс" or
            "/craft" or "/ремесло" or
            "/gacha" or "/гача" or
            "/abode_offering" or "/подношение_обители" or
            "/found_guardian_mantle" or "/учредить_хранителя" or
            "/guardian_trade" or "/торговля_хранителя" or
            "/guardian_social" or "/talk_guardian" or "/поговорить_с_хранителем" or "/общение_хранителя" or
            "/abode_residents" or "/обитатели_обители" or
            "/resident_interaction" or "/общение_резидента" or "/поговорить_с_резидентом" or "/история_резидента" or
            "/resident_transfer" or "/переход_резидента" or
            "/soul_relic_equip" or "/экипировать_реликвию" or
            "/soul_relic_unequip" or "/снять_реликвию" or
            "/shining_faction_founding" or "/основание_сияющей_фракции" or
            "/shining_faction_realignment" or "/перестройка_сияющей_фракции" or
            "/shining_faction_leadership" or "/смена_главы_сияющей_фракции" or
            "/shining_trade" or "/сияющая_торговля" or
            "/shining_treasury" or "/казначейство" or
            "/source_of_light" or "/источник_света" or
            "/afterlife_inbox" or "/уведомления_загробья" or
            "/spiritual_arts" or "/духовные_искусства";
    }

    private static bool OwnerMatches(PromptSessionSnapshot snapshot, string? ownerId) =>
        string.IsNullOrWhiteSpace(ownerId) ||
        string.Equals(snapshot.Owner.OwnerId, ownerId.Trim(), StringComparison.Ordinal);

    private static ExplorerCommandResult WithSession(
        ExplorerCommandResult result,
        UiPromptSession session,
        CommandExecutionState state,
        IReadOnlyList<UiPrompt> prompts,
        List<UiNotification>? extraNotifications = null)
    {
        var notifications = result.Notifications.ToList();
        if (extraNotifications != null)
            notifications.AddRange(extraNotifications);

        return new ExplorerCommandResult
        {
            Command = result.Command,
            State = state,
            Blocks = result.Blocks,
            Actions = result.Actions,
            Prompts = prompts.ToList(),
            Notifications = notifications,
            InteractiveSession = session
        };
    }

    private static ExplorerCommandResult MissingSessionResult(string sessionId) =>
        new()
        {
            Command = string.Empty,
            State = CommandExecutionState.Failed,
            Blocks =
            [
                new UiMessageBlock
                {
                    Severity = UiNotificationSeverity.Error,
                    Title = "Форма не найдена",
                    Message = "Эта игровая форма уже закрыта или устарела. Откройте действие заново."
                }
            ]
        };

    private static ExplorerCommandResult OwnerMismatchResult(PromptSessionSnapshot snapshot) =>
        new()
        {
            Command = snapshot.Result.Command,
            State = CommandExecutionState.Blocked,
            Blocks =
            [
                new UiMessageBlock
                {
                    Severity = UiNotificationSeverity.Warning,
                    Title = "Форма открыта в другой вкладке",
                    Message = "Вернитесь к той вкладке, где начали действие, или откройте форму заново."
                }
            ]
        };

    private static List<string> ValidateAnswers(IReadOnlyList<UiPrompt> prompts, Dictionary<string, JsonNode?> answers)
    {
        var errors = new List<string>();
        foreach (var prompt in prompts)
        {
            answers.TryGetValue(prompt.Id, out var value);
            if (prompt.Required && IsEmpty(value))
            {
                errors.Add($"Заполните обязательное поле: {PromptLabel(prompt)}.");
                continue;
            }

            if (value == null)
                continue;

            if (prompt is UiSelectionPrompt selection && !selection.AllowCustom)
            {
                var selected = ReadString(value);
                var option = selection.Options.FirstOrDefault(item =>
                    string.Equals(item.Value, selected, StringComparison.Ordinal));
                if (option == null)
                    errors.Add($"Выберите доступный вариант для поля: {PromptLabel(prompt)}.");
                else if (option.Disabled)
                    errors.Add($"Этот вариант сейчас недоступен: {PromptLabel(prompt)}.");
            }

            if (prompt is UiConfirmationPrompt &&
                (value is not JsonValue confirmationValue || !confirmationValue.TryGetValue<bool>(out _)))
            {
                errors.Add($"Подтверждение должно быть включено или выключено: {PromptLabel(prompt)}.");
            }
        }

        return errors;
    }

    private static string PromptLabel(UiPrompt prompt) =>
        string.IsNullOrWhiteSpace(prompt.Prompt)
            ? "поле формы"
            : prompt.Prompt.Trim();

    private static bool IsEmpty(JsonNode? value) =>
        value == null ||
        (value is JsonValue jsonValue &&
         jsonValue.TryGetValue<string>(out var text) &&
         string.IsNullOrWhiteSpace(text));

    private static string ReadString(JsonNode value) =>
        value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : value.ToJsonString();

    private static JsonObject AnswersToJson(Dictionary<string, JsonNode?> answers)
    {
        var root = new JsonObject();
        foreach (var (key, value) in answers)
            root[key] = value?.DeepClone();
        return root;
    }

    private sealed record PromptSessionSnapshot(
        UiPromptSession Session,
        ExplorerCommandResult Result,
        LocalUiSessionLockOwner Owner,
        bool RequiresLocalUiLock,
        DateTime ExpiresAtUtc);
}
