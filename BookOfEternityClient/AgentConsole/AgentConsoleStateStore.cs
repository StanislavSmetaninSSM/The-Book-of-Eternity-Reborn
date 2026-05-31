namespace BookOfEternityClient.AgentConsole;

public sealed class AgentConsoleStateStore
{
    public const int DefaultEventCapacity = 200;

    private readonly object _sync = new();
    private readonly Queue<AgentConsoleEvent> _events = new();
    private readonly int _eventCapacity;
    private readonly Func<DateTimeOffset> _utcNow;
    private AgentConsoleSnapshot? _currentSnapshot;
    private long _nextSequenceId;

    public AgentConsoleStateStore(
        int eventCapacity = DefaultEventCapacity,
        Func<DateTimeOffset>? utcNow = null)
    {
        if (eventCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventCapacity), eventCapacity, "Event capacity must be positive.");

        _eventCapacity = eventCapacity;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public AgentConsoleObservationState ReadState()
    {
        lock (_sync)
        {
            return new AgentConsoleObservationState
            {
                CurrentSnapshot = _currentSnapshot,
                Events = _events.ToArray(),
                ObservedAtUtc = _utcNow()
            };
        }
    }

    public AgentConsoleSnapshot? GetSnapshot()
    {
        lock (_sync)
            return _currentSnapshot;
    }

    public IReadOnlyList<AgentConsoleEvent> GetEvents()
    {
        lock (_sync)
            return _events.ToArray();
    }

    public AgentConsoleEvent UpdateSnapshot(AgentConsoleSnapshot snapshot, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_sync)
        {
            _currentSnapshot = snapshot;
            return AppendEventLocked(new AgentConsoleEvent
            {
                Kind = AgentConsoleEventKind.ScreenRendered,
                ScreenId = snapshot.ScreenId,
                InputKind = snapshot.InputKind,
                Message = message
            });
        }
    }

    public AgentConsoleEvent ClearSnapshot(string? message = null)
    {
        lock (_sync)
        {
            _currentSnapshot = null;
            return AppendEventLocked(new AgentConsoleEvent
            {
                Kind = AgentConsoleEventKind.StateChanged,
                Message = message
            });
        }
    }

    public AgentConsoleEvent AppendEvent(
        AgentConsoleEventKind kind,
        string? screenId = null,
        AgentConsoleInputKind? inputKind = null,
        string? message = null,
        AgentConsoleDiagnostic? diagnostic = null)
    {
        lock (_sync)
        {
            return AppendEventLocked(new AgentConsoleEvent
            {
                Kind = kind,
                ScreenId = screenId,
                InputKind = inputKind,
                Message = message,
                Diagnostic = diagnostic
            });
        }
    }

    private AgentConsoleEvent AppendEventLocked(AgentConsoleEvent agentEvent)
    {
        var sequenced = agentEvent with
        {
            SequenceId = ++_nextSequenceId,
            OccurredAtUtc = _utcNow()
        };

        _events.Enqueue(sequenced);
        while (_events.Count > _eventCapacity)
            _events.Dequeue();

        return sequenced;
    }
}
