using BookOfEternityClient.Core;
using System.Threading;

namespace BookOfEternityClient.AgentConsole;

public sealed class AgentConsoleLiveInputException : InvalidOperationException
{
    public AgentConsoleLiveInputException(AgentConsoleInputReadFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public AgentConsoleInputReadFailureReason Reason { get; }
}

public sealed class AgentConsoleLiveInputSource : IConsoleInputSource, IDisposable
{
    public const int DefaultMaxQueueLength = 1024;
    public static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(30);

    private readonly object _sync = new();
    private readonly Queue<QueuedInput> _queue = new();
    private readonly AgentConsoleStateStore _stateStore;
    private readonly TimeSpan _readTimeout;
    private readonly int _maxQueueLength;
    private QueuedInputKind? _activeReadKind;
    private AgentConsoleInputKind? _activeReadInputKind;
    private AgentConsoleSnapshot? _inputBlockSnapshot;
    private string? _inputBlockReason;
    private long _inputBlockVersion;
    private long _cancelSignalVersion;
    private bool _isShutdown;
    private string? _shutdownReason;

    public AgentConsoleLiveInputSource(
        AgentConsoleStateStore stateStore,
        TimeSpan? readTimeout = null,
        int maxQueueLength = DefaultMaxQueueLength)
    {
        ArgumentNullException.ThrowIfNull(stateStore);

        var effectiveReadTimeout = readTimeout ?? DefaultReadTimeout;
        if (effectiveReadTimeout != Timeout.InfiniteTimeSpan && effectiveReadTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(readTimeout), readTimeout, "Read timeout must be positive or Timeout.InfiniteTimeSpan.");
        if (maxQueueLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxQueueLength), maxQueueLength, "Queue length must be positive.");

        _stateStore = stateStore;
        _readTimeout = effectiveReadTimeout;
        _maxQueueLength = maxQueueLength;
    }

    public bool IsScripted => false;

    public bool KeyAvailable
    {
        get
        {
            lock (_sync)
            {
                return !_isShutdown &&
                       _queue.TryPeek(out var input) &&
                       input.Kind == QueuedInputKind.Key;
            }
        }
    }

    public AgentConsoleInputResult EnqueueKey(ConsoleKeyInfo key)
        => TryEnqueue(
            QueuedInput.ForKey(key),
            AgentConsoleInputKind.Key,
            "Queued key input.",
            screenId: _stateStore.GetSnapshot()?.ScreenId);

    public AgentConsoleInputResult EnqueueLine(string? line)
        => TryEnqueue(
            QueuedInput.ForLine(line ?? string.Empty),
            AgentConsoleInputKind.Text,
            "Queued text line input.",
            screenId: _stateStore.GetSnapshot()?.ScreenId);

    public AgentConsoleEvent PublishSnapshot(AgentConsoleSnapshot snapshot, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        AgentConsoleSnapshot effectiveSnapshot;
        lock (_sync)
        {
            effectiveSnapshot = _inputBlockSnapshot is not null && snapshot.AwaitingInput
                ? _inputBlockSnapshot with { UpdatedAtUtc = DateTimeOffset.UtcNow }
                : snapshot;
        }

        return _stateStore.UpdateSnapshot(effectiveSnapshot, message);
    }

    public IDisposable BeginInputBlockFromCurrentSnapshot(string? reason = null)
    {
        var snapshot = _stateStore.GetSnapshot() ?? BuildFallbackInputBlockSnapshot(reason);
        return BeginInputBlock(snapshot, reason);
    }

    public IDisposable BeginInputBlock(AgentConsoleSnapshot snapshot, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "Agent Console input is temporarily blocked."
            : reason.Trim();
        var blockSnapshot = BuildInputBlockSnapshot(snapshot, normalizedReason);
        long version;

        lock (_sync)
        {
            version = ++_inputBlockVersion;
            _inputBlockReason = normalizedReason;
            _inputBlockSnapshot = blockSnapshot;
        }

        _stateStore.UpdateSnapshot(blockSnapshot, normalizedReason);
        return new InputBlockScope(this, version);
    }

    public AgentConsoleInputResult TryQueueAction(AgentConsoleActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ActionId))
            return Reject(AgentConsoleInputRejectionCode.InvalidRequest, null, null, "Action id is required.");

        var snapshot = _stateStore.GetSnapshot();
        if (snapshot is null)
        {
            return Reject(
                AgentConsoleInputRejectionCode.NoSnapshot,
                null,
                null,
                $"Action '{request.ActionId}' cannot be queued because there is no current console snapshot.");
        }

        if (!string.IsNullOrWhiteSpace(request.ScreenId) &&
            !string.Equals(request.ScreenId, snapshot.ScreenId, StringComparison.Ordinal))
        {
            return Reject(
                AgentConsoleInputRejectionCode.ScreenMismatch,
                snapshot.InputKind,
                snapshot.ScreenId,
                $"Action '{request.ActionId}' targets screen '{request.ScreenId}', but the current screen is '{snapshot.ScreenId}'.");
        }

        if (request.InputKind.HasValue && request.InputKind.Value != snapshot.InputKind)
        {
            return Reject(
                AgentConsoleInputRejectionCode.InputKindMismatch,
                snapshot.InputKind,
                snapshot.ScreenId,
                $"Action '{request.ActionId}' targets input kind '{request.InputKind.Value}', but the current input kind is '{snapshot.InputKind}'.");
        }

        if (!snapshot.AwaitingInput)
        {
            return Reject(
                AgentConsoleInputRejectionCode.NotAwaitingInput,
                snapshot.InputKind,
                snapshot.ScreenId,
                $"Action '{request.ActionId}' cannot be queued because the current screen is not awaiting input.");
        }

        var actionIndex = FindActionIndex(snapshot.Actions, request.ActionId);
        if (actionIndex < 0)
        {
            return Reject(
                AgentConsoleInputRejectionCode.ActionMissing,
                snapshot.InputKind,
                snapshot.ScreenId,
                $"Action '{request.ActionId}' is not exposed by the current screen.");
        }

        var action = snapshot.Actions[actionIndex];
        if (!action.IsEnabled)
        {
            return Reject(
                AgentConsoleInputRejectionCode.ActionDisabled,
                snapshot.InputKind,
                snapshot.ScreenId,
                $"Action '{request.ActionId}' is disabled on the current screen.");
        }

        if (!TryResolveActionInput(snapshot, action, actionIndex, out var queuedInputs, out var resolvedInputKind, out var rejectionCode, out var rejectionMessage))
            return Reject(rejectionCode, snapshot.InputKind, snapshot.ScreenId, rejectionMessage);

        return TryEnqueue(
            queuedInputs,
            resolvedInputKind,
            $"Queued action '{request.ActionId}'.",
            snapshot.ScreenId);
    }

    public AgentConsoleInputResult TryQueueDefaultAction()
    {
        var snapshot = _stateStore.GetSnapshot();
        if (snapshot is null)
        {
            return Reject(
                AgentConsoleInputRejectionCode.NoSnapshot,
                null,
                null,
                "Cannot queue the current default action because there is no current console snapshot.");
        }

        if (!snapshot.AwaitingInput)
        {
            return Reject(
                AgentConsoleInputRejectionCode.NotAwaitingInput,
                snapshot.InputKind,
                snapshot.ScreenId,
                $"Cannot queue the current default action because screen '{snapshot.ScreenId}' is not awaiting input.");
        }

        var actionIndex = FindDefaultActionIndex(snapshot.Actions);
        if (actionIndex < 0)
        {
            return Reject(
                AgentConsoleInputRejectionCode.ActionMissing,
                snapshot.InputKind,
                snapshot.ScreenId,
                $"Screen '{snapshot.ScreenId}' does not expose an enabled default action.");
        }

        var action = snapshot.Actions[actionIndex];
        if (!TryResolveActionInput(
                snapshot,
                action,
                actionIndex,
                out var queuedInputs,
                out var resolvedInputKind,
                out var rejectionCode,
                out var rejectionMessage))
        {
            return Reject(rejectionCode, snapshot.InputKind, snapshot.ScreenId, rejectionMessage);
        }

        return TryEnqueue(
            queuedInputs,
            resolvedInputKind,
            $"Queued current default action '{action.Id}'.",
            snapshot.ScreenId);
    }

    public AgentConsoleInputResult TryQueueReturnToGameLoopStep()
    {
        var snapshot = _stateStore.GetSnapshot();
        if (snapshot is null)
        {
            return Reject(
                AgentConsoleInputRejectionCode.NoSnapshot,
                null,
                null,
                "Cannot queue a return-to-game-loop step because there is no current console snapshot.");
        }

        if (IsGameLoopSnapshot(snapshot))
        {
            return Reject(
                AgentConsoleInputRejectionCode.InvalidRequest,
                snapshot.InputKind,
                snapshot.ScreenId,
                "Agent Console is already at the game-loop prompt.");
        }

        if (!IsLocalCommandSnapshot(snapshot))
        {
            return Reject(
                AgentConsoleInputRejectionCode.InvalidRequest,
                snapshot.InputKind,
                snapshot.ScreenId,
                $"Screen '{snapshot.ScreenId}' is not a local command screen that can be safely unwound automatically.");
        }

        if (!snapshot.AwaitingInput)
        {
            return Reject(
                AgentConsoleInputRejectionCode.NotAwaitingInput,
                snapshot.InputKind,
                snapshot.ScreenId,
                $"Screen '{snapshot.ScreenId}' is not awaiting input.");
        }

        var action = FindReturnToGameLoopAction(snapshot);
        if (action is null)
        {
            return Reject(
                AgentConsoleInputRejectionCode.ActionMissing,
                snapshot.InputKind,
                snapshot.ScreenId,
                $"Screen '{snapshot.ScreenId}' does not expose a safe back/close/continue action.");
        }

        var actionIndex = FindActionIndex(snapshot.Actions, action.Id);
        if (!TryResolveActionInput(
                snapshot,
                action,
                actionIndex,
                out var queuedInputs,
                out var resolvedInputKind,
                out var rejectionCode,
                out var rejectionMessage,
                preferMenuIndexInput: true))
        {
            return Reject(rejectionCode, snapshot.InputKind, snapshot.ScreenId, rejectionMessage);
        }

        return TryEnqueue(
            queuedInputs,
            resolvedInputKind,
            $"Queued return-to-game-loop action '{action.Id}'.",
            snapshot.ScreenId);
    }

    public ConsoleKeyInfo ReadKey(bool intercept = true)
        => ReadNext(QueuedInputKind.Key, AgentConsoleInputKind.Key, "ReadKey").Key;

    public string? ReadLine()
        => ReadNext(QueuedInputKind.Line, AgentConsoleInputKind.Text, "ReadLine").Line;

    public void CancelPendingReads(string? reason = null)
    {
        lock (_sync)
        {
            _cancelSignalVersion++;
            Monitor.PulseAll(_sync);
        }

        _stateStore.AppendEvent(
            AgentConsoleEventKind.StateChanged,
            message: string.IsNullOrWhiteSpace(reason)
                ? "Agent Console live input reads were cancelled."
                : reason);
    }

    public void Shutdown(string? reason = null)
    {
        var appended = false;
        lock (_sync)
        {
            if (!_isShutdown)
            {
                _isShutdown = true;
                _shutdownReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
                appended = true;
            }

            Monitor.PulseAll(_sync);
        }

        if (appended)
        {
            _stateStore.AppendEvent(
                AgentConsoleEventKind.StateChanged,
                message: _shutdownReason ?? "Agent Console live input source shut down.");
        }
    }

    public void AssertCompleted()
    {
        // Live sessions are open-ended; queued input is consumed by the active console flow.
    }

    public void Dispose() => Shutdown("Agent Console live input source disposed.");

    private AgentConsoleInputResult TryEnqueue(
        QueuedInput queuedInput,
        AgentConsoleInputKind inputKind,
        string acceptedMessage,
        string? screenId)
        => TryEnqueue(
            [queuedInput],
            inputKind,
            acceptedMessage,
            screenId);

    private AgentConsoleInputResult TryEnqueue(
        IReadOnlyList<QueuedInput> queuedInputs,
        AgentConsoleInputKind inputKind,
        string acceptedMessage,
        string? screenId)
    {
        AgentConsoleInputRejectionCode rejectionCode = AgentConsoleInputRejectionCode.None;
        string? rejectionMessage = null;
        var queuedInputKind = queuedInputs.Count > 0 ? queuedInputs[0].Kind : QueuedInputKind.Key;

        lock (_sync)
        {
            if (_isShutdown)
            {
                rejectionCode = AgentConsoleInputRejectionCode.InputClosed;
                rejectionMessage = _shutdownReason is null
                    ? "Agent Console live input source is shut down."
                    : $"Agent Console live input source is shut down: {_shutdownReason}";
            }
            else if (queuedInputs.Count == 0)
            {
                rejectionCode = AgentConsoleInputRejectionCode.InvalidRequest;
                rejectionMessage = "Agent Console action resolved to no input.";
            }
            else if (_inputBlockSnapshot is not null)
            {
                rejectionCode = AgentConsoleInputRejectionCode.NotAwaitingInput;
                rejectionMessage = _inputBlockReason ?? "Agent Console input is temporarily blocked.";
            }
            else if (_queue.Count + queuedInputs.Count > _maxQueueLength)
            {
                rejectionCode = AgentConsoleInputRejectionCode.QueueFull;
                rejectionMessage = $"Agent Console live input queue is full at {_maxQueueLength} item(s).";
            }
            else if (queuedInputs.Any(input => input.Kind != queuedInputKind))
            {
                rejectionCode = AgentConsoleInputRejectionCode.InvalidRequest;
                rejectionMessage = "Agent Console cannot queue mixed input kinds for one action.";
            }
            else if (_activeReadKind.HasValue && _activeReadKind.Value != queuedInputKind)
            {
                rejectionCode = AgentConsoleInputRejectionCode.InputKindMismatch;
                var expected = _activeReadInputKind ?? AgentConsoleInputKind.None;
                rejectionMessage =
                    $"Agent Console is waiting for {expected.ToString().ToLowerInvariant()} input; " +
                    $"{inputKind.ToString().ToLowerInvariant()} input cannot be queued until that read completes.";
            }
            else if (!CanQueueForCurrentSnapshot(queuedInputKind, inputKind, out rejectionCode, out rejectionMessage))
            {
                // Rejection details are provided by the helper.
            }
            else
            {
                foreach (var queuedInput in queuedInputs)
                    _queue.Enqueue(queuedInput);
                Monitor.PulseAll(_sync);
            }
        }

        return rejectionCode == AgentConsoleInputRejectionCode.None
            ? Accept(inputKind, screenId, acceptedMessage)
            : Reject(rejectionCode, inputKind, screenId, rejectionMessage!);
    }

    private QueuedInput ReadNext(QueuedInputKind expectedKind, AgentConsoleInputKind expectedInputKind, string operation)
    {
        var waitsIndefinitely = _readTimeout == Timeout.InfiniteTimeSpan;
        var deadlineUtc = waitsIndefinitely ? DateTime.MaxValue : DateTime.UtcNow + _readTimeout;

        lock (_sync)
        {
            var observedCancelVersion = _cancelSignalVersion;
            var previousReadKind = _activeReadKind;
            var previousReadInputKind = _activeReadInputKind;
            _activeReadKind = expectedKind;
            _activeReadInputKind = expectedInputKind;
            try
            {
                while (true)
                {
                    if (_isShutdown)
                        ThrowReadFailure(AgentConsoleInputReadFailureReason.Shutdown, expectedInputKind, $"{operation} was unblocked because the live input source shut down.");

                    if (_cancelSignalVersion != observedCancelVersion)
                        ThrowReadFailure(AgentConsoleInputReadFailureReason.Cancelled, expectedInputKind, $"{operation} was cancelled before input was queued.");

                    if (_queue.TryPeek(out var input))
                    {
                        if (input.Kind != expectedKind)
                        {
                            ThrowReadFailure(
                                AgentConsoleInputReadFailureReason.InputKindMismatch,
                                expectedInputKind,
                                $"{operation} expected {expectedKind.ToString().ToLowerInvariant()} input, but the next queued input is {input.Kind.ToString().ToLowerInvariant()}.");
                        }

                        var dequeued = _queue.Dequeue();
                        MarkCurrentSnapshotInputConsumed(dequeued);
                        return dequeued;
                    }

                    if (waitsIndefinitely)
                    {
                        Monitor.Wait(_sync);
                        continue;
                    }

                    var remaining = deadlineUtc - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                        ThrowReadFailure(AgentConsoleInputReadFailureReason.Timeout, expectedInputKind, $"{operation} timed out waiting for Agent Console input.");

                    Monitor.Wait(_sync, remaining);
                }
            }
            finally
            {
                _activeReadKind = previousReadKind;
                _activeReadInputKind = previousReadInputKind;
            }
        }
    }

    private bool CanQueueForCurrentSnapshot(
        QueuedInputKind queuedInputKind,
        AgentConsoleInputKind inputKind,
        out AgentConsoleInputRejectionCode rejectionCode,
        out string? rejectionMessage)
    {
        rejectionCode = AgentConsoleInputRejectionCode.None;
        rejectionMessage = null;

        if (_activeReadKind.HasValue)
            return true;

        var snapshot = _stateStore.GetSnapshot();
        if (snapshot is null)
            return true;

        if (!snapshot.AwaitingInput)
        {
            rejectionCode = AgentConsoleInputRejectionCode.NotAwaitingInput;
            rejectionMessage = $"Agent Console screen '{snapshot.ScreenId}' is not awaiting input.";
            return false;
        }

        if (IsInputCompatibleWithSnapshot(queuedInputKind, snapshot.InputKind))
            return true;

        rejectionCode = AgentConsoleInputRejectionCode.InputKindMismatch;
        rejectionMessage =
            $"Agent Console screen '{snapshot.ScreenId}' is waiting for {snapshot.InputKind.ToString().ToLowerInvariant()} input; " +
            $"{inputKind.ToString().ToLowerInvariant()} input cannot be queued.";
        return false;
    }

    private void EndInputBlock(long version)
    {
        lock (_sync)
        {
            if (_inputBlockVersion != version)
                return;

            _inputBlockSnapshot = null;
            _inputBlockReason = null;
        }
    }

    private static AgentConsoleSnapshot BuildInputBlockSnapshot(
        AgentConsoleSnapshot snapshot,
        string reason)
    {
        var now = DateTimeOffset.UtcNow;
        var plainText = string.IsNullOrWhiteSpace(snapshot.PlainText)
            ? reason
            : snapshot.PlainText;

        if (!plainText.Contains(reason, StringComparison.OrdinalIgnoreCase))
            plainText += Environment.NewLine + Environment.NewLine + reason;

        return snapshot with
        {
            AwaitingInput = false,
            InputKind = AgentConsoleInputKind.None,
            Actions = [],
            Prompt = null,
            RenderedAtUtc = snapshot.RenderedAtUtc == default ? now : snapshot.RenderedAtUtc,
            UpdatedAtUtc = now
        };
    }

    private static AgentConsoleSnapshot BuildFallbackInputBlockSnapshot(string? reason)
    {
        var now = DateTimeOffset.UtcNow;
        var plainText = string.IsNullOrWhiteSpace(reason)
            ? "Agent Console input is temporarily blocked."
            : reason.Trim();
        return new AgentConsoleSnapshot
        {
            ScreenId = "agent-console-input-blocked",
            Mode = AgentConsoleMode.Loading,
            Title = "Ожидание",
            PlainText = plainText,
            AwaitingInput = false,
            InputKind = AgentConsoleInputKind.None,
            RenderedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static bool IsInputCompatibleWithSnapshot(QueuedInputKind queuedInputKind, AgentConsoleInputKind snapshotInputKind)
        => queuedInputKind switch
        {
            QueuedInputKind.Line => snapshotInputKind == AgentConsoleInputKind.Text,
            QueuedInputKind.Key => snapshotInputKind is AgentConsoleInputKind.Key
                or AgentConsoleInputKind.MenuSelection
                or AgentConsoleInputKind.Confirmation,
            _ => false
        };

    private void MarkCurrentSnapshotInputConsumed(QueuedInput consumedInput)
    {
        var snapshot = _stateStore.GetSnapshot();
        if (snapshot is not { AwaitingInput: true })
            return;

        if (string.Equals(snapshot.ScreenId, "game-loop", StringComparison.Ordinal))
        {
            var now = DateTimeOffset.UtcNow;
            if (consumedInput.Kind == QueuedInputKind.Line && IsLocalCommandLine(consumedInput.Line))
            {
                _stateStore.UpdateSnapshot(new AgentConsoleSnapshot
                {
                    ScreenId = "command-processing",
                    Mode = AgentConsoleMode.Loading,
                    Title = "Команда выполняется",
                    PlainText = "Локальная команда выполняется. Клиент готовит экран результата без отправки хода GM.",
                    AwaitingInput = false,
                    InputKind = AgentConsoleInputKind.None,
                    RenderedAtUtc = now,
                    UpdatedAtUtc = now
                }, "Input consumed for game-loop; processing local command.");
                return;
            }

            _stateStore.UpdateSnapshot(new AgentConsoleSnapshot
            {
                ScreenId = "turn-preparing",
                Mode = AgentConsoleMode.Loading,
                Title = "Ход принят",
                PlainText = "Игровое действие принято. Клиент готовит запрос для GM и собирает контекст хода.",
                AwaitingInput = false,
                InputKind = AgentConsoleInputKind.None,
                RenderedAtUtc = now,
                UpdatedAtUtc = now
            }, "Input consumed for game-loop; preparing turn.");
            return;
        }

        _stateStore.UpdateSnapshot(snapshot with
        {
            AwaitingInput = false,
            InputKind = AgentConsoleInputKind.None,
            Actions = [],
            Prompt = null,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        }, $"Input consumed for {snapshot.ScreenId}.");
    }

    private void ThrowReadFailure(
        AgentConsoleInputReadFailureReason reason,
        AgentConsoleInputKind expectedInputKind,
        string message)
    {
        var snapshot = _stateStore.GetSnapshot();
        _stateStore.AppendEvent(
            AgentConsoleEventKind.Failure,
            screenId: snapshot?.ScreenId,
            inputKind: expectedInputKind,
            message: message,
            diagnostic: new AgentConsoleDiagnostic
            {
                Severity = AgentConsoleDiagnosticSeverity.Error,
                Code = $"agent-console-input-{reason.ToString().ToLowerInvariant()}",
                Message = message
            });

        throw new AgentConsoleLiveInputException(reason, message);
    }

    private AgentConsoleInputResult Accept(AgentConsoleInputKind inputKind, string? screenId, string message)
    {
        var agentEvent = _stateStore.AppendEvent(
            AgentConsoleEventKind.InputAccepted,
            screenId: screenId,
            inputKind: inputKind,
            message: message);

        return new AgentConsoleInputResult
        {
            Accepted = true,
            RejectionCode = AgentConsoleInputRejectionCode.None,
            Message = message,
            Event = agentEvent
        };
    }

    private AgentConsoleInputResult Reject(
        AgentConsoleInputRejectionCode rejectionCode,
        AgentConsoleInputKind? inputKind,
        string? screenId,
        string message)
    {
        var agentEvent = _stateStore.AppendEvent(
            AgentConsoleEventKind.InputRejected,
            screenId: screenId,
            inputKind: inputKind,
            message: message,
            diagnostic: new AgentConsoleDiagnostic
            {
                Severity = AgentConsoleDiagnosticSeverity.Error,
                Code = $"agent-console-input-{rejectionCode.ToString().ToLowerInvariant()}",
                Message = message
            });

        return new AgentConsoleInputResult
        {
            Accepted = false,
            RejectionCode = rejectionCode,
            Message = message,
            Event = agentEvent
        };
    }

    private static int FindActionIndex(IReadOnlyList<AgentConsoleAction> actions, string actionId)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            if (string.Equals(actions[index].Id, actionId, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    private static int FindDefaultActionIndex(IReadOnlyList<AgentConsoleAction> actions)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            if (actions[index].IsEnabled && actions[index].IsDefault)
                return index;
        }

        return -1;
    }

    private static bool IsGameLoopSnapshot(AgentConsoleSnapshot snapshot)
        => string.Equals(snapshot.ScreenId, "game-loop", StringComparison.Ordinal);

    private static bool IsLocalCommandSnapshot(AgentConsoleSnapshot snapshot)
        => snapshot.ScreenId.StartsWith("explorer-command-", StringComparison.Ordinal) ||
           snapshot.ScreenId.StartsWith("explorer-selection-", StringComparison.Ordinal);

    private static AgentConsoleAction? FindReturnToGameLoopAction(AgentConsoleSnapshot snapshot)
    {
        if (snapshot.InputKind == AgentConsoleInputKind.Key)
        {
            return snapshot.Actions.FirstOrDefault(action =>
                       action.IsEnabled &&
                       (IsContinueAction(action) || action.IsDefault)) ??
                   snapshot.Actions.FirstOrDefault(action => action.IsEnabled);
        }

        if (snapshot.InputKind is not (AgentConsoleInputKind.MenuSelection or AgentConsoleInputKind.Confirmation))
            return null;

        return snapshot.Actions.FirstOrDefault(action => action.IsEnabled && IsBackOrCloseAction(action)) ??
               snapshot.Actions.FirstOrDefault(action => action.IsEnabled && IsContinueAction(action));
    }

    private static bool IsContinueAction(AgentConsoleAction action)
        => string.Equals(action.Id, "continue", StringComparison.OrdinalIgnoreCase) ||
           ContainsReturnToken(action.Label, "продолж") ||
           ContainsReturnToken(action.Label, "continue");

    private static bool IsBackOrCloseAction(AgentConsoleAction action)
        => ContainsReturnToken(action.Label, "назад") ||
           ContainsReturnToken(action.Label, "закрыть") ||
           ContainsReturnToken(action.Label, "вернуться") ||
           ContainsReturnToken(action.Label, "к списку") ||
           ContainsReturnToken(action.Label, "к обучен") ||
           ContainsReturnToken(action.Label, "к учител") ||
           ContainsReturnToken(action.Label, "к наставник") ||
           ContainsReturnToken(action.Label, "к игре") ||
           ContainsReturnToken(action.Label, "back") ||
           ContainsReturnToken(action.Label, "close") ||
           ContainsReturnToken(action.Label, "return");

    private static bool ContainsReturnToken(string? label, string token)
        => !string.IsNullOrWhiteSpace(label) &&
           label.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static bool IsLocalCommandLine(string? line)
        => !string.IsNullOrWhiteSpace(line) &&
           line.TrimStart().StartsWith("/", StringComparison.Ordinal);

    private static bool TryResolveActionInput(
        AgentConsoleSnapshot snapshot,
        AgentConsoleAction action,
        int actionIndex,
        out IReadOnlyList<QueuedInput> queuedInputs,
        out AgentConsoleInputKind inputKind,
        out AgentConsoleInputRejectionCode rejectionCode,
        out string rejectionMessage,
        bool preferMenuIndexInput = false)
    {
        queuedInputs = [];
        inputKind = AgentConsoleInputKind.Key;
        rejectionCode = AgentConsoleInputRejectionCode.None;
        rejectionMessage = string.Empty;

        if (snapshot.InputKind == AgentConsoleInputKind.Text)
        {
            var text = !string.IsNullOrWhiteSpace(action.InputValue)
                ? action.InputValue
                : actionIndex >= 0 &&
                       actionIndex < (snapshot.Prompt?.Choices.Count ?? 0) &&
                       !string.IsNullOrWhiteSpace(snapshot.Prompt?.Choices[actionIndex])
                    ? snapshot.Prompt!.Choices[actionIndex]
                    : action.Label;

            if (string.IsNullOrWhiteSpace(text))
            {
                rejectionCode = AgentConsoleInputRejectionCode.UnsupportedActionResolution;
                rejectionMessage = $"Action '{action.Id}' cannot be resolved to text input.";
                return false;
            }

            queuedInputs = [QueuedInput.ForLine(text)];
            inputKind = AgentConsoleInputKind.Text;
            return true;
        }

        if (snapshot.InputKind is not (AgentConsoleInputKind.Key
            or AgentConsoleInputKind.MenuSelection
            or AgentConsoleInputKind.Confirmation))
        {
            rejectionCode = AgentConsoleInputRejectionCode.UnsupportedActionResolution;
            rejectionMessage = $"Action '{action.Id}' cannot be resolved for input kind '{snapshot.InputKind}'.";
            return false;
        }

        inputKind = snapshot.InputKind;

        if (preferMenuIndexInput && snapshot.InputKind == AgentConsoleInputKind.MenuSelection)
        {
            if (TryBuildMenuInputValueKeys(action.InputValue, appendEnter: true, out var preferredInputValueKeys))
            {
                queuedInputs = preferredInputValueKeys;
                return true;
            }

            if (actionIndex >= 0 && actionIndex < 9)
            {
                var digit = (char)('1' + actionIndex);
                queuedInputs =
                [
                    QueuedInput.ForKey(new ConsoleKeyInfo(digit, (ConsoleKey)((int)ConsoleKey.D1 + actionIndex), shift: false, alt: false, control: false)),
                    QueuedInput.ForKey(new ConsoleKeyInfo('\0', ConsoleKey.Enter, shift: false, alt: false, control: false))
                ];
                return true;
            }

            if (actionIndex >= 0 && actionIndex < snapshot.Actions.Count)
            {
                queuedInputs = BuildMenuNavigationInputs(snapshot, actionIndex, appendEnter: true);
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(action.Shortcut))
        {
            if (!TryParseConsoleKey(action.Shortcut, out var key))
            {
                rejectionCode = AgentConsoleInputRejectionCode.UnsupportedActionShortcut;
                rejectionMessage = $"Action '{action.Id}' has unsupported shortcut '{action.Shortcut}'.";
                return false;
            }

            queuedInputs = [QueuedInput.ForKey(key)];
            return true;
        }

        if ((snapshot.SelectedIndex.HasValue && snapshot.SelectedIndex.Value == actionIndex) || action.IsDefault)
        {
            queuedInputs = [QueuedInput.ForKey(new ConsoleKeyInfo('\0', ConsoleKey.Enter, shift: false, alt: false, control: false))];
            return true;
        }

        if (snapshot.InputKind == AgentConsoleInputKind.MenuSelection &&
            TryBuildMenuInputValueKeys(action.InputValue, appendEnter: true, out var inputValueKeys))
        {
            queuedInputs = inputValueKeys;
            return true;
        }

        if (snapshot.InputKind == AgentConsoleInputKind.MenuSelection &&
            actionIndex >= 0 &&
            actionIndex < 9)
        {
            var digit = (char)('1' + actionIndex);
            queuedInputs =
            [
                QueuedInput.ForKey(new ConsoleKeyInfo(digit, (ConsoleKey)((int)ConsoleKey.D1 + actionIndex), shift: false, alt: false, control: false)),
                QueuedInput.ForKey(new ConsoleKeyInfo('\0', ConsoleKey.Enter, shift: false, alt: false, control: false))
            ];
            return true;
        }

        if (snapshot.InputKind == AgentConsoleInputKind.MenuSelection &&
            actionIndex >= 0 &&
            actionIndex < snapshot.Actions.Count)
        {
            queuedInputs = BuildMenuNavigationInputs(snapshot, actionIndex, appendEnter: true);
            return true;
        }

        rejectionCode = AgentConsoleInputRejectionCode.UnsupportedActionResolution;
        rejectionMessage = $"Action '{action.Id}' cannot be resolved to a safe console input.";
        return false;
    }

    private static IReadOnlyList<QueuedInput> BuildMenuNavigationInputs(
        AgentConsoleSnapshot snapshot,
        int targetIndex,
        bool appendEnter)
    {
        var currentIndex = snapshot.SelectedIndex.GetValueOrDefault(0);
        if (currentIndex < 0 || currentIndex >= snapshot.Actions.Count)
            currentIndex = 0;

        var queuedInputs = new List<QueuedInput>();
        var movementKey = targetIndex >= currentIndex ? ConsoleKey.DownArrow : ConsoleKey.UpArrow;
        var movementCount = Math.Abs(targetIndex - currentIndex);
        for (var i = 0; i < movementCount; i++)
            queuedInputs.Add(QueuedInput.ForKey(CreateNavigationKey(movementKey)));

        if (appendEnter)
            queuedInputs.Add(QueuedInput.ForKey(new ConsoleKeyInfo('\0', ConsoleKey.Enter, shift: false, alt: false, control: false)));
        return queuedInputs;
    }

    private static bool TryBuildMenuInputValueKeys(
        string? inputValue,
        bool appendEnter,
        out IReadOnlyList<QueuedInput> queuedInputs)
    {
        queuedInputs = [];
        if (string.IsNullOrWhiteSpace(inputValue))
            return false;

        var value = inputValue.Trim();
        if (value.Any(ch => !char.IsDigit(ch)))
            return false;

        var keys = new List<QueuedInput>(value.Length + (appendEnter ? 1 : 0));
        foreach (var ch in value)
            keys.Add(QueuedInput.ForKey(CreateDigitKey(ch)));

        if (appendEnter)
            keys.Add(QueuedInput.ForKey(new ConsoleKeyInfo('\0', ConsoleKey.Enter, shift: false, alt: false, control: false)));
        queuedInputs = keys;
        return true;
    }

    private static ConsoleKeyInfo CreateDigitKey(char digit)
    {
        if (digit < '0' || digit > '9')
            throw new ArgumentOutOfRangeException(nameof(digit), digit, "Only decimal digit keys are supported.");

        var key = digit == '0'
            ? ConsoleKey.D0
            : (ConsoleKey)((int)ConsoleKey.D1 + (digit - '1'));
        return new ConsoleKeyInfo(digit, key, shift: false, alt: false, control: false);
    }

    private static ConsoleKeyInfo CreateNavigationKey(ConsoleKey key) =>
        key switch
        {
            ConsoleKey.UpArrow => new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, shift: false, alt: false, control: false),
            ConsoleKey.DownArrow => new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, shift: false, alt: false, control: false),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Only menu navigation keys are supported.")
        };

    private static bool TryParseConsoleKey(string shortcut, out ConsoleKeyInfo keyInfo)
    {
        keyInfo = default;
        var normalized = shortcut.Trim();
        if (normalized.Length == 0)
            return false;

        if (normalized.Length == 1)
        {
            var ch = normalized[0];
            if (char.IsLetter(ch) &&
                Enum.TryParse<ConsoleKey>(char.ToUpperInvariant(ch).ToString(), ignoreCase: false, out var letterKey))
            {
                keyInfo = new ConsoleKeyInfo(char.ToLowerInvariant(ch), letterKey, shift: false, alt: false, control: false);
                return true;
            }

            if (char.IsDigit(ch) &&
                Enum.TryParse<ConsoleKey>("D" + ch, ignoreCase: false, out var digitKey))
            {
                keyInfo = new ConsoleKeyInfo(ch, digitKey, shift: false, alt: false, control: false);
                return true;
            }
        }

        switch (normalized.ToLowerInvariant())
        {
            case "space":
            case "spacebar":
                keyInfo = new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, shift: false, alt: false, control: false);
                return true;
            case "up":
            case "uparrow":
                keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, shift: false, alt: false, control: false);
                return true;
            case "down":
            case "downarrow":
                keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, shift: false, alt: false, control: false);
                return true;
            case "left":
            case "leftarrow":
                keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, shift: false, alt: false, control: false);
                return true;
            case "right":
            case "rightarrow":
                keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, shift: false, alt: false, control: false);
                return true;
            case "enter":
            case "return":
                keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.Enter, shift: false, alt: false, control: false);
                return true;
            case "escape":
            case "esc":
                keyInfo = new ConsoleKeyInfo('\0', ConsoleKey.Escape, shift: false, alt: false, control: false);
                return true;
            case "tab":
                keyInfo = new ConsoleKeyInfo('\t', ConsoleKey.Tab, shift: false, alt: false, control: false);
                return true;
            case "backspace":
                keyInfo = new ConsoleKeyInfo('\b', ConsoleKey.Backspace, shift: false, alt: false, control: false);
                return true;
            default:
                if (Enum.TryParse<ConsoleKey>(normalized, ignoreCase: true, out var parsed))
                {
                    keyInfo = new ConsoleKeyInfo('\0', parsed, shift: false, alt: false, control: false);
                    return true;
                }

                return false;
        }
    }

    private readonly record struct QueuedInput(QueuedInputKind Kind, ConsoleKeyInfo Key, string Line)
    {
        public static QueuedInput ForKey(ConsoleKeyInfo key) => new(QueuedInputKind.Key, key, string.Empty);

        public static QueuedInput ForLine(string line) => new(QueuedInputKind.Line, default, line);
    }

    private enum QueuedInputKind
    {
        Key,
        Line
    }

    private sealed class InputBlockScope : IDisposable
    {
        private AgentConsoleLiveInputSource? _owner;
        private readonly long _version;

        public InputBlockScope(AgentConsoleLiveInputSource owner, long version)
        {
            _owner = owner;
            _version = version;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.EndInputBlock(_version);
        }
    }
}
