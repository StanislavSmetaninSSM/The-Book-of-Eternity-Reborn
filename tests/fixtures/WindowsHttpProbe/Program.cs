using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

if (args.Length != 3)
    throw new ArgumentException("Usage: WindowsHttpProbe <owned-data-root> <frontend-assets> <new-report-path>");
var root = Path.GetFullPath(args[0]);
var assets = Path.GetFullPath(args[1]);
var report = Path.GetFullPath(args[2]);
if (!Directory.Exists(root) || File.Exists(report))
    throw new InvalidOperationException("An existing owned root and an unused report path are required.");
var observations = new List<Observation>();
using var logger = new EvidenceLoggerProvider();
var started = DateTime.UtcNow;
var stopped = false;
var disposed = false;
string? fatal = null;
var app = LocalWebUiHost.Build([], new(root, "http://127.0.0.1:0", assets));
try
{
    app.Services.GetRequiredService<ILoggerFactory>().AddProvider(logger);
    await app.StartAsync();
    var url = app.Urls.Single();
    Console.WriteLine($"READY pid={Environment.ProcessId} url={url}");
    using var client = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(45) };
    string[] routes = ["/api/main-menu", "/api/session", "/api/game-screen", "/api/audio/settings", "/api/client/settings"];
    for (var batch = 0; batch < 2; batch++)
    {
        var results = await Task.WhenAll(routes.Select(route => ReadAsync(client, route, $"parallel-{batch}")));
        observations.AddRange(results);
        Console.WriteLine(JsonSerializer.Serialize(results));
    }
    foreach (var route in routes)
        observations.Add(await ReadAsync(client, route, "serial"));
}
catch (Exception ex)
{
    fatal = ex.ToString();
}
finally
{
    try
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await app.StopAsync(cancellation.Token);
        stopped = true;
    }
    finally
    {
        await app.DisposeAsync();
        disposed = true;
        var evidence = new { StartedUtc = started, FinishedUtc = DateTime.UtcNow, Root = root,
            ProcessId = Environment.ProcessId, Stopped = stopped, Disposed = disposed,
            Fatal = fatal, Observations = observations, ServerLogs = logger.Entries.ToArray() };
        await using var output = new FileStream(report, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(output, evidence, new JsonSerializerOptions { WriteIndented = true });
    }
}
Console.WriteLine($"STOPPED={stopped} DISPOSED={disposed} REPORT={report}");
return fatal == null ? 0 : 1;

static async Task<Observation> ReadAsync(HttpClient client, string route, string batch)
{
    var clock = Stopwatch.StartNew();
    try
    {
        using var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();
        return new(batch, route, (int)response.StatusCode, clock.ElapsedMilliseconds,
            response.IsSuccessStatusCode ? null : body, null);
    }
    catch (Exception ex)
    {
        return new(batch, route, null, clock.ElapsedMilliseconds, null, ex.ToString());
    }
}

internal sealed record Observation(string Batch, string Route, int? Status, long Milliseconds, string? ErrorBody, string? Exception);
internal sealed record LogEntry(DateTime Utc, string Category, string Level, int EventId, string Message, string? Exception);
internal sealed class EvidenceLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();
    public ILogger CreateLogger(string categoryName) => new EvidenceLogger(categoryName, Entries);
    public void Dispose() { }
    private sealed class EvidenceLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(level))
                entries.Enqueue(new(DateTime.UtcNow, category, level.ToString(), eventId.Id, formatter(state, exception), exception?.ToString()));
        }
    }
}
