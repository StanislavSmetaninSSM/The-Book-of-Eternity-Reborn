using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

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

    public ExplorerWebPromptSessionService(
        FileSystemManager fs,
        LocalUiSessionLockService? lockService = null,
        TimeProvider? timeProvider = null)
    {
        _fs = fs;
        _lockService = lockService ?? new LocalUiSessionLockService(fs, timeProvider);
        _timeProvider = timeProvider ?? TimeProvider.System;
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
                            Title = "Активный ход GM",
                            Message = pending.Message
                        }
                    ],
                    Notifications =
                    [
                        new UiNotification
                        {
                            Severity = UiNotificationSeverity.Warning,
                            Title = "Форма не открыта",
                            Message = "Browser-write заблокирован до завершения текущего GM-turn/rollback протокола."
                        }
                    ]
                };
            }

            var lockResult = await _lockService.AcquireOrRefreshAsync(owner, $"Browser prompt session: {result.Command}");
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
                            Title = "Локальная UI-блокировка",
                            Message = lockResult.BlockerMessage
                        }
                    ],
                    Notifications =
                    [
                        new UiNotification
                        {
                            Severity = UiNotificationSeverity.Warning,
                            Title = "Форма не открыта",
                            Message = "Другой интерфейс удерживает право локальной записи."
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

        _sessions.TryRemove(request.SessionId, out _);
        if (snapshot.RequiresLocalUiLock)
            await _lockService.ReleaseAsync(snapshot.Owner);

        var submittedAnswers = AnswersToJson(answers);
        var blocks = snapshot.Result.Blocks.ToList();
        blocks.Add(new UiMessageBlock
        {
            Severity = UiNotificationSeverity.Success,
            Title = "Ответы формы приняты",
            Message = "Браузерная prompt-session завершена без обращения к Spectre.Console. Прикладная запись будет подключаться отдельными миграционными задачами."
        });
        blocks.Add(new UiRawJsonBlock
        {
            Title = "JSON: отправленные ответы формы",
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
                    Message = $"Команда {snapshot.Result.Command} получила ответы браузера."
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
                    Message = $"Браузерная prompt-session для {snapshot.Result.Command} отменена."
                }
            ],
            Notifications =
            [
                new UiNotification
                {
                    Severity = UiNotificationSeverity.Info,
                    Title = "Форма отменена",
                    Message = "Локальная UI-блокировка освобождена, если она была нужна."
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
        var token = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return token is
            "/validate" or "/валидация" or
            "/world_setup" or "/настройка_мира" or
            "/distribute" or "/распределить" or
            "/companion_directive" or "/директива_компаньону" or
            "/faction_directive" or "/директива_фракции" or
            "/craft" or "/ремесло" or
            "/abode_offering" or "/подношение_обители" or
            "/found_guardian_mantle" or "/учредить_хранителя" or
            "/spiritual_action" or "/духовное_действие";
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
                    Message = $"Prompt-session {sessionId} отсутствует или устарела."
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
                    Title = "Форма принадлежит другому UI",
                    Message = $"Prompt-session принадлежит {snapshot.Owner.OwnerLabel}. Повторите действие из того же браузерного владельца."
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
                errors.Add($"Поле {prompt.Id} обязательно.");
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
                    errors.Add($"Поле {prompt.Id}: неизвестный вариант '{selected}'.");
                else if (option.Disabled)
                    errors.Add($"Поле {prompt.Id}: вариант '{selected}' сейчас недоступен.");
            }

            if (prompt is UiConfirmationPrompt &&
                (value is not JsonValue confirmationValue || !confirmationValue.TryGetValue<bool>(out _)))
            {
                errors.Add($"Поле {prompt.Id}: подтверждение должно быть boolean.");
            }
        }

        return errors;
    }

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
