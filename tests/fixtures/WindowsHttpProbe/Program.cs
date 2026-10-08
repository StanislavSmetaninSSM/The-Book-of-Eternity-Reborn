using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

if (args.Length is not (3 or 4) || args.Length == 4 && args[3] != "--save-load")
    throw new ArgumentException("Usage: WindowsHttpProbe <owned-data-root> <frontend-assets> <new-report-path> [--save-load]");
var root = Path.GetFullPath(args[0]);
var assets = Path.GetFullPath(args[1]);
var report = Path.GetFullPath(args[2]);
if (!Directory.Exists(root) || File.Exists(report))
    throw new InvalidOperationException("An existing owned root and an unused report path are required.");
var observations = new List<Observation>();
var saveLoad = new List<object>();
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
    if (args.Length == 4)
    {
        using var saves = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(90) };
        using var created = await saves.PostAsJsonAsync("/api/saves/create", new { saveName = "http_owned_probe_20261008" });
        var createdBody = await created.Content.ReadAsStringAsync();
        var createdJson = JsonNode.Parse(createdBody)!;
        saveLoad.Add(new { Operation = "create", Status = (int)created.StatusCode, Body = createdJson.DeepClone() });
        if (!created.IsSuccessStatusCode || createdJson["disposition"]?.ToString() != "Committed" || createdJson["continuationBlocked"]?.GetValue<bool>() != false)
            throw new InvalidOperationException("Owned save creation did not confirm unblocked commit; do not retry.");
        var saveId = createdJson["createdSaveId"]!.GetValue<string>();
        using var loaded = await saves.PostAsJsonAsync("/api/saves/load", new { saveId, operationId = Guid.NewGuid().ToString("N") });
        var loadedBody = await loaded.Content.ReadAsStringAsync();
        var loadedJson = JsonNode.Parse(loadedBody)!;
        saveLoad.Add(new { Operation = "load", Status = (int)loaded.StatusCode, Body = loadedJson.DeepClone() });
        if (!loaded.IsSuccessStatusCode || loadedJson["disposition"]?.ToString() != "Committed" || loadedJson["continuationBlocked"]?.GetValue<bool>() != false)
            throw new InvalidOperationException("Owned load did not confirm unblocked commit; do not retry.");
        foreach (var required in new[] { "menu", "session", "settings", "audio", "game" })
            if (loadedJson["state"]?[required] == null) throw new InvalidOperationException("Missing actual Load bundle member: " + required);
        observations.AddRange(await Task.WhenAll(routes.Select(route => ReadAsync(client, route, "after-save-load"))));
    }
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
            Fatal = fatal, Observations = observations, SaveLoad = saveLoad, ServerLogs = logger.Entries.ToArray() };
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
        if (response.IsSuccessStatusCode)
        {
            var parsed = JsonNode.Parse(body) ?? throw new InvalidDataException("Successful response is empty.");
            var required = route switch
            {
                "/api/main-menu" => parsed["session"],
                "/api/session" => parsed["status"],
                "/api/game-screen" => parsed["soul"],
                "/api/audio/settings" => parsed["musicEnabled"],
                "/api/client/settings" => parsed["language"],
                _ => throw new InvalidOperationException("Unreviewed probe route.")
            };
            if (required == null) throw new InvalidDataException("Successful response lacks required payload: " + route);
        }
        return new(batch, route, (int)response.StatusCode, clock.ElapsedMilliseconds,
            response.IsSuccessStatusCode ? null : body, null, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant());
    }
    catch (Exception ex)
    {
        return new(batch, route, null, clock.ElapsedMilliseconds, null, ex.ToString(), null);
    }
}

internal sealed record Observation(string Batch, string Route, int? Status, long Milliseconds, string? ErrorBody, string? Exception, string? PayloadSHA256);
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
