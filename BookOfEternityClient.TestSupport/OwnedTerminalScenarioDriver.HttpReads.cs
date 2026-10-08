using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.WebUi;

namespace BookOfEternityClient.Tests;

internal static partial class OwnedTerminalScenarioDriver
{
    private static async Task RunOriginalOwnerHttpReadsAsync(string folder, object host, Type type,
        Func<object, Task<JsonElement>> rpc, Dictionary<string, object?> evidence)
    {
        void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        var main = (GmSessionRunCoordinator)type.GetField("_mainRun", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        var original = (IOwnedTerminalSession)type.GetField("_pty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        var identity = main.Identity;
        var queue = Path.Combine(folder, "queue");
        var responses = new List<object>();
        var failures = new List<string>();
        evidence["HttpResponses"] = responses;
        evidence["ReadFailures"] = failures;
        evidence["OriginalRunningIdentity"] = identity;
        evidence["ModelRequests"] = 0;
        evidence["NoReadyOverride"] = true;
        Require(GmSessionRunCoordinator.Current == null, "HTTP fixture inherited original operation authority.");
        Require(main.Record!.Disposition == GmSessionRunDisposition.Running && main.RetainsAuthority,
            "Actual original production owner is not Running.");
        Require(Directory.GetDirectories(queue, "request-*").Length == 0, "Idle fixture dispatched a prompt.");

        // Observe real immutable terminal receipts under their actual owner lock.
        // Reflection is observation only; no metadata or admission is fabricated.
        MainOperationClose[] ClosedReceipts()
        {
            var sync = typeof(GmSessionRunCoordinator).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
            lock (sync)
            {
                var pins = (Dictionary<string, GmSessionRunCoordinator.RemotePin>)typeof(GmSessionRunCoordinator)
                    .GetField("_remotePins", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
                Require(pins.Values.All(p => p.State == MainOperationState.ClosedObserved), "Original operation pin remains unresolved.");
                return pins.Values.Select(p => (MainOperationClose)typeof(GmSessionRunCoordinator.RemotePin)
                    .GetField("_close", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(p)!).ToArray();
            }
        }

        try
        {
            var assets = Path.Combine(folder, "http-assets");
            Directory.CreateDirectory(assets);
            File.WriteAllText(Path.Combine(assets, "index.html"), "<html><body>Owned HTTP fixture</body></html>");
            await using var app = LocalWebUiHost.Build([], new(identity.RootKey, "http://127.0.0.1:0", assets));
            await app.StartAsync();
            try
            {
                var before = ClosedReceipts().Select(p => p.PinId).ToHashSet(StringComparer.Ordinal);
                using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(20) };
                var routes = new[] { "/api/session", "/api/game-screen", "/api/lifecycle/dashboard", "/api/lifecycle/validate" };
                async Task Read(string stage, string route)
                {
                    using var response = route.EndsWith("/validate", StringComparison.Ordinal)
                        ? await http.PostAsync(route, null) : await http.GetAsync(route);
                    var body = await response.Content.ReadAsStringAsync();
                    lock (responses) responses.Add(new { Stage = stage, Path = route, Status = (int)response.StatusCode, Body = body });
                    string? failure = null;
                    if (!response.IsSuccessStatusCode) failure = route + " returned " + (int)response.StatusCode;
                    else
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(body);
                            var dto = doc.RootElement;
                            var valid = route switch
                            {
                                "/api/session" => dto.GetProperty("status").GetString() == "ok" && dto.GetProperty("gameSessionExists").GetBoolean(),
                                "/api/game-screen" => dto.GetProperty("schemaVersion").GetInt32() == 2 && dto.GetProperty("soul").GetProperty("name").GetString() == "Контрольная душа",
                                "/api/lifecycle/dashboard" => dto.GetProperty("soul").GetProperty("name").GetString() == "Контрольная душа" && dto.GetProperty("session").GetProperty("gameSessionExists").GetBoolean(),
                                "/api/lifecycle/validate" => dto.GetProperty("issues").ValueKind == JsonValueKind.Array && dto.GetProperty("groups").ValueKind == JsonValueKind.Array,
                                "/api/audio/settings" => dto.GetProperty("musicEnabled").ValueKind is JsonValueKind.True or JsonValueKind.False,
                                "/api/client/settings" => dto.GetProperty("schemaVersion").GetInt32() == 1,
                                _ => false
                            };
                            if (!valid || body.Contains("browser_validation_exception", StringComparison.Ordinal)) failure = route + " returned an incomplete or swallowed-error DTO";
                        }
                        catch (Exception ex) { failure = route + " invalid DTO: " + ex.Message; }
                    }
                    if (failure != null) lock (failures) failures.Add(failure);
                }

                foreach (var route in routes) await Read("serial", route);
                await Task.WhenAll(routes.Concat(new[] { "/api/audio/settings", "/api/client/settings" }).Select(route => Read("parallel", route)));
                var closed = ClosedReceipts().Where(p => !before.Contains(p.PinId)).ToArray();
                evidence["ActualClosedHttpPins"] = closed;
                if (closed.Length != 10 || closed.Any(p => p.Identity != identity || p.Outcome != MainOperationOutcome.Completed || p.ClosingFailed))
                    failures.Add("Each complete HTTP DTO must have one matching original Completed close receipt.");
                Require(ReferenceEquals(original, type.GetField("_pty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)) &&
                    main.Identity == identity && main.Record!.Disposition == GmSessionRunDisposition.Running,
                    "HTTP reads changed the original terminal, epoch or generation.");
                Require(Directory.GetDirectories(queue, "request-*").Length == 0, "HTTP fixture dispatched a relay prompt.");
                evidence["IdleQueueRequests"] = 0;
            }
            finally
            {
                await app.StopAsync();
                evidence["WebStopped"] = true;
            }
        }
        finally
        {
            // Even causal RED must close the real idle consumer before retiring
            // its retained original Bridge. The outer driver also owns cleanup.
            try
            {
                var start = new ProcessStartInfo("/usr/bin/python3") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { Path.Combine(folder, "ship/gm-relay/relay_worker.py"), "close", queue }) start.ArgumentList.Add(arg);
                using var worker = Process.Start(start)!;
                var output = worker.StandardOutput.ReadToEndAsync();
                var error = worker.StandardError.ReadToEndAsync();
                await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));
                await File.WriteAllTextAsync(Path.Combine(folder, "http-relay-close.log"), (await output) + (await error));
                Require(worker.ExitCode == 0, "Idle relay queue close failed.");
                for (var i = 0; i < 200 && !File.Exists(Path.Combine(queue, "closed.json")); i++) await Task.Delay(10);
                using var closed = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(queue, "closed.json")));
                Require(new[] { "ExecutionDisabled", "ChildExited", "IoDrained" }.All(k => closed.RootElement.GetProperty(k).GetBoolean()), "Idle relay closure/I-O unconfirmed.");
                evidence["QueueClosedBeforeOriginalStop"] = closed.RootElement.Clone();
            }
            finally
            {
                var stopped = await rpc(new { command = "shutdown", rootKey = identity.RootKey, expectedMainIdentity = identity });
                evidence["OriginalShutdown"] = stopped;
                Require(stopped.GetProperty("ok").GetBoolean() && main.Record!.Disposition == GmSessionRunDisposition.Stopped &&
                    !main.RetainsAuthority && original.RootExited.IsCompleted, "Original stop/I-O/Stopped ACK unconfirmed.");
                evidence["OriginalStoppedRecord"] = main.Record;
            }
        }
        Require(failures.Count == 0, "Causal HTTP admission failure: " + string.Join("; ", failures));
    }
}
