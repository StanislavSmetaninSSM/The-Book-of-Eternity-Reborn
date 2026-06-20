using BookOfEternityClient.Core;

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
        => _stateStore.UpdateSnapshot(snapshot, message);

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

        if (!TryResolveActionInput(snapshot, action, actionIndex, out var queuedInput, out var resolvedInputKind, out var rejectionCode, out var rejectionMessage))
            return Reject(rejectionCode, snapshot.InputKind, snapshot.ScreenId, rejectionMessage);

        return TryEnqueue(
            queuedInput,
            resolvedInputKind,
            $"Queued action '{request.ActionId}'.",
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
    {
        AgentConsoleInputRejectionCode rejectionCode = AgentConsoleInputRejectionCode.None;
        string? rejectionMessage = null;

        lock (_sync)
        {
            if (_isShutdown)
            {
                rejectionCode = AgentConsoleInputRejectionCode.InputClosed;
                rejectionMessage = _shutdownReason is null
                    ? "Agent Console live input source is shut down."
                    : $"Agent Console live input source is shut down: {_shutdownReason}";
            }
            else if (_queue.Count >= _maxQueueLength)
            {
                rejectionCode = AgentConsoleInputRejectionCode.QueueFull;
                rejectionMessage = $"Agent Console live input queue is full at {_maxQueueLength} item(s).";
            }
            else if (_activeReadKind.HasValue && _activeReadKind.Value != queuedInput.Kind)
            {
                rejectionCode = AgentConsoleInputRejectionCode.InputKindMismatch;
                var expected = _activeReadInputKind ?? AgentConsoleInputKind.None;
                rejectionMessage =
                    $"Agent Console is waiting for {expected.ToString().ToLowerInvariant()} input; " +
                    $"{inputKind.ToString().ToLowerInvariant()} input cannot be queued until that read completes.";
            }
            else if (!CanQueueForCurrentSnapshot(queuedInput.Kind, inputKind, out rejectionCode, out rejectionMessage))
            {
                // Rejection details are provided by the helper.
            }
            else
            {
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
                        MarkCurrentSnapshotInputConsumed();
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

    private static bool IsInputCompatibleWithSnapshot(QueuedInputKind queuedInputKind, AgentConsoleInputKind snapshotInputKind)
        => queuedInputKind switch
        {
            QueuedInputKind.Line => snapshotInputKind == AgentConsoleInputKind.Text,
            QueuedInputKind.Key => snapshotInputKind is AgentConsoleInputKind.Key
                or AgentConsoleInputKind.MenuSelection
                or AgentConsoleInputKind.Confirmation,
            _ => false
        };

    private void MarkCurrentSnapshotInputConsumed()
    {
        var snapshot = _stateStore.GetSnapshot();
        if (snapshot is not { AwaitingInput: true })
            return;

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

    private static bool TryResolveActionInput(
        AgentConsoleSnapshot snapshot,
        AgentConsoleAction action,
        int actionIndex,
        out QueuedInput queuedInput,
        out AgentConsoleInputKind inputKind,
        out AgentConsoleInputRejectionCode rejectionCode,
        out string rejectionMessage)
    {
        queuedInput = default;
        inputKind = AgentConsoleInputKind.Key;
        rejectionCode = AgentConsoleInputRejectionCode.None;
        rejectionMessage = string.Empty;

        if (snapshot.InputKind is not (AgentConsoleInputKind.Key
            or AgentConsoleInputKind.MenuSelection
            or AgentConsoleInputKind.Confirmation))
        {
            rejectionCode = AgentConsoleInputRejectionCode.UnsupportedActionResolution;
            rejectionMessage = $"Action '{action.Id}' cannot be resolved for input kind '{snapshot.InputKind}'.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(action.Shortcut))
        {
            if (!TryParseConsoleKey(action.Shortcut, out var key))
            {
                rejectionCode = AgentConsoleInputRejectionCode.UnsupportedActionShortcut;
                rejectionMessage = $"Action '{action.Id}' has unsupported shortcut '{action.Shortcut}'.";
                return false;
            }

            queuedInput = QueuedInput.ForKey(key);
            return true;
        }

        if ((snapshot.SelectedIndex.HasValue && snapshot.SelectedIndex.Value == actionIndex) || action.IsDefault)
        {
            queuedInput = QueuedInput.ForKey(new ConsoleKeyInfo('\0', ConsoleKey.Enter, shift: false, alt: false, control: false));
            return true;
        }

        if (snapshot.InputKind == AgentConsoleInputKind.MenuSelection &&
            actionIndex >= 0 &&
            actionIndex < 9)
        {
            var digit = (char)('1' + actionIndex);
            queuedInput = QueuedInput.ForKey(new ConsoleKeyInfo(digit, (ConsoleKey)((int)ConsoleKey.D1 + actionIndex), shift: false, alt: false, control: false));
            return true;
        }

        rejectionCode = AgentConsoleInputRejectionCode.UnsupportedActionResolution;
        rejectionMessage = $"Action '{action.Id}' cannot be resolved to a safe console input.";
        return false;
    }

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
}
