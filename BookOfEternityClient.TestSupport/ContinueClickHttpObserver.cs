using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

// Loaded only by the explicitly owned Continue diagnostic's DOTNET_STARTUP_HOOKS.
// The actual Program and LocalWebUiHost remain unchanged; this observes requests.
public static class StartupHook
{
    private static readonly object Gate = new();
    private static string? _path;
    private static ContinueTaskWaitObserver? _tasks;
    private static readonly List<IDisposable> Subscriptions = [];

    public static void Initialize()
    {
        _path = Environment.GetEnvironmentVariable("BOE_TEST_CONTINUE_HTTP_OBSERVER_PATH");
        if (string.IsNullOrEmpty(_path)) return;
        if (!Path.IsPathFullyQualified(_path) || File.Exists(_path))
            throw new InvalidOperationException("Continue observer requires a fresh owned absolute output path.");
        Write("ObserverStarted", null);
        var taskPath = Environment.GetEnvironmentVariable("BOE_TEST_STARTUP_TASK_OBSERVER_PATH");
        if (!string.IsNullOrEmpty(taskPath)) _tasks = new(taskPath);
        Subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(new ListenerObserver()));
    }

    private static void Write(string eventName, HttpContext? context, string? error = null)
    {
        var row = new
        {
            Event = eventName, Pid = Environment.ProcessId, Utc = DateTimeOffset.UtcNow,
            StopwatchTicks = Stopwatch.GetTimestamp(), StopwatchFrequency = Stopwatch.Frequency,
            TraceIdentifier = context?.TraceIdentifier, Method = context?.Request.Method,
            Path = context?.Request.Path.Value, Query = context?.Request.QueryString.Value,
            UserAgent = context?.Request.Headers.UserAgent.ToString(),
            Endpoint = context?.GetEndpoint()?.DisplayName, Status = context?.Response.StatusCode,
            ConnectionId = context?.Connection.Id, RemotePort = context?.Connection.RemotePort,
            ActivityTraceId = Activity.Current?.TraceId.ToString(),
            Error = error
        };
        lock (Gate) File.AppendAllText(_path!, JsonSerializer.Serialize(row) + Environment.NewLine);
    }

    private sealed class ListenerObserver : IObserver<DiagnosticListener>
    {
        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.AspNetCore")
                Subscriptions.Add(listener.Subscribe(new RequestObserver()));
        }
        public void OnCompleted() { }
        public void OnError(Exception error) => Console.Error.WriteLine("Continue listener observer: " + error);
    }

    private sealed class RequestObserver : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> item)
        {
            try
            {
                var context = item.Value as HttpContext
                    ?? item.Value?.GetType().GetProperty("HttpContext")?.GetValue(item.Value) as HttpContext
                    ?? item.Value?.GetType().GetProperty("httpContext")?.GetValue(item.Value) as HttpContext;
                if (context is not null)
                {
                    _tasks?.ObserveRequest(item.Key, context);
                    Write(item.Key, context);
                }
            }
            catch (Exception error)
            {
                // Observer failure is retained separately; never replace a host result.
                Console.Error.WriteLine("Continue request observer: " + error);
                try { Write("ObserverError", null, error.ToString()); } catch { }
            }
        }
        public void OnCompleted() { }
        public void OnError(Exception error) => Console.Error.WriteLine("Continue request observer: " + error);
    }
}
