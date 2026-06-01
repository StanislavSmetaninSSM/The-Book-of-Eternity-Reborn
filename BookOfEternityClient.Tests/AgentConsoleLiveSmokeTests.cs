using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AgentConsoleLiveSmokeTests : IDisposable
{
#if DEBUG
    private const string TestBuildConfiguration = "Debug";
#else
    private const string TestBuildConfiguration = "Release";
#endif

    private readonly string _tempRoot;

    public AgentConsoleLiveSmokeTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "boe-agent-console-smoke-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task AgentConsoleLiveControl_MainMenuSnapshotActionAndShutdown_UsesDisposableSandbox()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureGameSessionPath = Path.Combine(repoRoot, "FileSystemExample", "game_session");
        using var sandbox = ConsoleE2ESandbox.CreateFromFixture(
            fixtureGameSessionPath,
            _tempRoot,
            preserveArtifacts: true);

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        var token = "agent-console-smoke-" + Guid.NewGuid().ToString("N");
        using var process = StartAgentConsoleClient(repoRoot, sandbox.BasePath, url, token);
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(url),
                Timeout = TimeSpan.FromSeconds(2)
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var initialSnapshot = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(30),
                snapshot => string.Equals(snapshot["screenId"]?.GetValue<string>(), "main-menu", StringComparison.Ordinal));

            Assert.Equal("menu", initialSnapshot["mode"]!.GetValue<string>());
            Assert.True(initialSnapshot["awaitingInput"]!.GetValue<bool>());
            Assert.Equal("menuSelection", initialSnapshot["inputKind"]!.GetValue<string>());

            var initialSelectedIndex = initialSnapshot["selectedIndex"]?.GetValue<int>() ?? -1;
            var exitAction = FindExitAction(initialSnapshot);
            var exitActionId = exitAction.Action["id"]!.GetValue<string>();
            Assert.StartsWith("option-", exitActionId, StringComparison.Ordinal);

            using var keyResponse = await client.PostAsJsonAsync("/api/agent-console/key", new { key = "Down" });
            Assert.True(
                keyResponse.IsSuccessStatusCode,
                $"Expected key endpoint success, got {(int)keyResponse.StatusCode}: {await keyResponse.Content.ReadAsStringAsync()}");

            var afterKeySnapshot = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(10),
                snapshot => (snapshot["selectedIndex"]?.GetValue<int>() ?? -1) != initialSelectedIndex);
            Assert.Equal("main-menu", afterKeySnapshot["screenId"]!.GetValue<string>());

            using var selectExitResponse = await client.PostAsJsonAsync("/api/agent-console/action", new
            {
                actionId = exitActionId,
                screenId = afterKeySnapshot["screenId"]!.GetValue<string>(),
                inputKind = "menuSelection"
            });
            Assert.True(
                selectExitResponse.IsSuccessStatusCode,
                $"Expected action endpoint to select exit, got {(int)selectExitResponse.StatusCode}: {await selectExitResponse.Content.ReadAsStringAsync()}");

            var exitSelectedSnapshot = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(10),
                snapshot => (snapshot["selectedIndex"]?.GetValue<int>() ?? -1) == exitAction.Index);
            Assert.Equal(exitActionId, exitSelectedSnapshot["actions"]![exitAction.Index]!["id"]!.GetValue<string>());

            var events = JsonNode.Parse(await client.GetStringAsync("/api/agent-console/events"))!.AsArray();
            Assert.Contains(events, node => node?["kind"]?.GetValue<string>() == "screenRendered");
            Assert.Contains(events, node => node?["kind"]?.GetValue<string>() == "inputAccepted");

            using var activateExitResponse = await client.PostAsJsonAsync("/api/agent-console/action", new
            {
                actionId = exitActionId,
                screenId = exitSelectedSnapshot["screenId"]!.GetValue<string>(),
                inputKind = "menuSelection"
            });
            Assert.True(
                activateExitResponse.IsSuccessStatusCode,
                $"Expected action endpoint to activate exit, got {(int)activateExitResponse.StatusCode}: {await activateExitResponse.Content.ReadAsStringAsync()}");

            var waitForExitTask = process.WaitForExitAsync();
            var exited = await Task.WhenAny(waitForExitTask, Task.Delay(TimeSpan.FromSeconds(30)));
            if (exited != waitForExitTask)
                Assert.Fail($"Agent Console smoke process did not exit after selecting Exit. Sandbox: {sandbox.BasePath}");

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stdout.txt"), stdout);
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stderr.txt"), stderr);

            Assert.True(
                process.ExitCode == 0,
                $"Agent Console smoke process exited with {process.ExitCode}.{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}{Environment.NewLine}STDOUT tail:{Environment.NewLine}{Tail(stdout, 2000)}");
        }
        finally
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort cleanup; assertions above record the failed workflow.
                }
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static Process StartAgentConsoleClient(string repoRoot, string sandboxBasePath, string url, string token)
    {
        var projectPath = Path.Combine(repoRoot, "BookOfEternityClient", "BookOfEternityClient.csproj");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.StartInfo.ArgumentList.Add("run");
        process.StartInfo.ArgumentList.Add("--project");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.ArgumentList.Add("--configuration");
        process.StartInfo.ArgumentList.Add(TestBuildConfiguration);
        process.StartInfo.ArgumentList.Add("--no-build");
        process.StartInfo.ArgumentList.Add("--no-restore");
        process.StartInfo.ArgumentList.Add("--");
        process.StartInfo.ArgumentList.Add(sandboxBasePath);
        process.StartInfo.ArgumentList.Add("--agent-console");
        process.StartInfo.ArgumentList.Add("--agent-url");
        process.StartInfo.ArgumentList.Add(url);
        process.StartInfo.ArgumentList.Add("--agent-token");
        process.StartInfo.ArgumentList.Add(token);
        process.StartInfo.ArgumentList.Add("--plain-output");
        process.StartInfo.Environment["NO_COLOR"] = "1";

        Assert.True(process.Start(), "Failed to start dotnet run for Agent Console live smoke test.");
        return process;
    }

    private static async Task<JsonObject> WaitForSnapshotAsync(
        HttpClient client,
        TimeSpan timeout,
        Func<JsonObject, bool>? predicate = null)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        Exception? lastException = null;
        string? lastBody = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync("/api/agent-console/snapshot");
                lastBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode &&
                    !string.IsNullOrWhiteSpace(lastBody) &&
                    !string.Equals(lastBody.Trim(), "null", StringComparison.OrdinalIgnoreCase))
                {
                    var snapshot = JsonNode.Parse(lastBody)!.AsObject();
                    if (predicate == null || predicate(snapshot))
                        return snapshot;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastException = ex;
            }

            await Task.Delay(100);
        }

        Assert.Fail(
            $"Timed out waiting for Agent Console snapshot. Last body: {lastBody ?? "<none>"}. Last exception: {lastException?.Message ?? "<none>"}");
        throw new UnreachableException();
    }

    private static (JsonObject Action, int Index) FindExitAction(JsonObject snapshot)
    {
        var actions = snapshot["actions"]!.AsArray();
        for (var index = 0; index < actions.Count; index++)
        {
            var action = actions[index]!.AsObject();
            var label = action["label"]!.GetValue<string>();
            if (label.Contains("Exit", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("Выход", StringComparison.OrdinalIgnoreCase))
            {
                return (action, index);
            }
        }

        Assert.Fail("Expected main menu snapshot to expose an Exit action.");
        throw new UnreachableException();
    }

    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "FileSystemExample", "game_session")) &&
                File.Exists(Path.Combine(dir.FullName, "BookOfEternityClient", "BookOfEternityClient.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root for Agent Console smoke test.");
    }

    private static string Tail(string value, int maxCharacters)
    {
        if (value.Length <= maxCharacters)
            return value;

        return value[^maxCharacters..];
    }
}
