using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

// Diagnostic-only task wait registration evidence. No task/awaiter is invoked,
// replaced, completed or retained; IDs and the real registration stack are copied.
internal sealed class ContinueTaskWaitObserver : EventListener
{
    private const int MaxRows = 2048, MaxBytes = 4 * 1024 * 1024, MaxFrames = 48;
    private readonly object _gate = new();
    private readonly Dictionary<string, Route> _routes = [];
    private readonly Dictionary<int, List<Route>> _observedTasks = [];
    private readonly string? _path;
    private int _rows, _bytes, _dropped, _truncatedStacks, _errors;
    [ThreadStatic] private static bool _recording;
    private sealed record Route(string TraceIdentifier, string Path, string ActivityTraceId);

    internal ContinueTaskWaitObserver(string path)
    {
        if (!Path.IsPathFullyQualified(path) || File.Exists(path))
            throw new InvalidOperationException("Task observer requires a fresh owned output path.");
        _path = path;
        Append(new { Event = "TaskObserverStarted", Pid = Environment.ProcessId,
            Utc = DateTimeOffset.UtcNow, StopwatchTicks = Stopwatch.GetTimestamp(),
            StopwatchFrequency = Stopwatch.Frequency, MaxRows, MaxBytes, MaxFrames,
            TplKeywords = 3, ActivityFlowKeywordsEnabled = false,
            InferenceLimit = "WaitEnd observes continuation/wait return; this is not a heap task-state graph." });
        foreach (var source in EventSource.GetSources()) Enable(source);
    }

    private void Enable(EventSource source)
    {
        if (_path is not null && source.Name == "System.Threading.Tasks.TplEventSource")
            EnableEvents(source, EventLevel.Verbose, (EventKeywords)3);
    }
    protected override void OnEventSourceCreated(EventSource source) => Enable(source);

    internal void ObserveRequest(string name, HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (path is not ("/api/main-menu" or "/api/session" or "/api/game-screen" or
            "/api/audio/settings" or "/api/client/settings" or "/api/explorer/command-coverage")) return;
        if (!context.Request.Headers.UserAgent.ToString().Contains("Chrome/")) return;
        var trace = Activity.Current?.TraceId.ToString();
        if (string.IsNullOrEmpty(trace)) return;
        _recording = true;
        try { lock (_gate)
        {
            if (name.EndsWith("HttpRequestIn.Start"))
                _routes[trace] = new(context.TraceIdentifier, path, trace);
            if (name.EndsWith("HttpRequestIn.Stop"))
            {
                File.AppendAllText(_path!, JsonSerializer.Serialize(new {
                    Event = "TaskObserverRouteSummary", Pid = Environment.ProcessId,
                    Utc = DateTimeOffset.UtcNow, StopwatchTicks = Stopwatch.GetTimestamp(),
                    TraceIdentifier = context.TraceIdentifier, Path = path, ActivityTraceId = trace,
                    Rows = _rows, Bytes = _bytes, DroppedRows = _dropped,
                    TruncatedStacks = _truncatedStacks, Errors = _errors,
                    CaptureIncomplete = _dropped > 0 || _truncatedStacks > 0 || _errors > 0
                }) + Environment.NewLine);
                _routes.Remove(trace);
            }
        } }
        finally { _recording = false; }
    }

    protected override void OnEventWritten(EventWrittenEventArgs data)
    {
        if (_path is null || _recording || data.EventName is not ("TaskWaitBegin" or "TaskWaitEnd")) return;
        _recording = true;
        try
        {
            var payload = new Dictionary<string, object?>();
            for (var index = 0; index < (data.PayloadNames?.Count ?? 0); index++)
                payload[data.PayloadNames![index]] = data.Payload![index];
            if (!payload.TryGetValue("TaskID", out var value) || value is not int taskId)
            { Interlocked.Increment(ref _errors); return; }
            lock (_gate)
            {
                Route? route;
                var trace = Activity.Current?.TraceId.ToString() ?? "";
                var current = _routes.TryGetValue(trace, out route);
                _observedTasks.TryGetValue(taskId, out var candidates);
                if (!current && (data.EventName != "TaskWaitEnd" || candidates is null)) return;
                var ambiguous = !current && candidates!.Count > 1;
                if (!current && !ambiguous) route = candidates![0];
                if (_rows >= MaxRows || _bytes >= MaxBytes) { _dropped++; return; }
                var frames = data.EventName == "TaskWaitBegin" ? new StackTrace(1, false).GetFrames() : [];
                if (frames.Length > MaxFrames) _truncatedStacks++;
                var row = new { Event = data.EventName, Pid = Environment.ProcessId,
                    Utc = DateTimeOffset.UtcNow, StopwatchTicks = Stopwatch.GetTimestamp(), StopwatchFrequency = Stopwatch.Frequency,
                    ManagedThreadId = Environment.CurrentManagedThreadId, TraceIdentifier = route?.TraceIdentifier,
                    Path = route?.Path, ActivityTraceId = route?.ActivityTraceId,
                    Correlation = current ? "CurrentRequestActivity" : ambiguous ? "AmbiguousObservedTaskID" : "PreviouslyObservedTaskID",
                    RequestCandidates = candidates?.ToArray(),
                    Payload = payload, EventActivityId = data.ActivityId, RelatedActivityId = data.RelatedActivityId,
                    RegistrationFramesTruncated = frames.Length > MaxFrames,
                    RegistrationFrames = frames.Take(MaxFrames).Select(frame => new {
                        Type = frame.GetMethod()?.DeclaringType?.FullName, Method = frame.GetMethod()?.Name,
                        ILOffset = frame.GetILOffset() }).ToArray() };
                if (Append(row) && data.EventName == "TaskWaitBegin")
                {
                    if (candidates is null) _observedTasks[taskId] = candidates = [];
                    if (!candidates.Contains(route!)) candidates.Add(route!);
                }
                if (data.EventName == "TaskWaitEnd" && !ambiguous)
                {
                    candidates?.Remove(route!);
                    if (candidates?.Count == 0) _observedTasks.Remove(taskId);
                }
            }
        }
        catch (Exception error)
        {
            Interlocked.Increment(ref _errors);
            Console.Error.WriteLine("Continue task observer: " + error);
        }
        finally { _recording = false; }
    }

    private bool Append(object row)
    {
        var text = JsonSerializer.Serialize(row) + Environment.NewLine;
        var bytes = Encoding.UTF8.GetByteCount(text);
        if (_rows >= MaxRows || _bytes + bytes > MaxBytes) { _dropped++; return false; }
        File.AppendAllText(_path!, text); _rows++; _bytes += bytes; return true;
    }
}
