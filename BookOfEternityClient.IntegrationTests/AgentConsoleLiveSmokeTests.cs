using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "ProcessIntegration")]
[Trait("Category", "E2E")]
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

            var events = JsonNode.Parse(await client.GetStringAsync("/api/agent-console/events"))!.AsArray();
            Assert.Contains(events, node => node?["kind"]?.GetValue<string>() == "screenRendered");
            Assert.Contains(events, node => node?["kind"]?.GetValue<string>() == "inputAccepted");

            using var activateExitResponse = await client.PostAsJsonAsync("/api/agent-console/action", new
            {
                actionId = exitActionId,
                screenId = afterKeySnapshot["screenId"]!.GetValue<string>(),
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

    [Fact]
    public async Task AgentConsoleLiveControl_ContinuePublishesInGameTextPromptSnapshot()
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

            var mainMenu = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(30),
                snapshot => string.Equals(snapshot["screenId"]?.GetValue<string>(), "main-menu", StringComparison.Ordinal));

            var continueAction = mainMenu["actions"]!.AsArray()[0]!.AsObject();
            Assert.Contains("Продолжить", continueAction["label"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

            using var continueResponse = await client.PostAsJsonAsync("/api/agent-console/action", new
            {
                actionId = continueAction["id"]!.GetValue<string>(),
                screenId = mainMenu["screenId"]!.GetValue<string>(),
                inputKind = "menuSelection"
            });
            Assert.True(
                continueResponse.IsSuccessStatusCode,
                $"Expected Continue action endpoint success, got {(int)continueResponse.StatusCode}: {await continueResponse.Content.ReadAsStringAsync()}");

            var inGameSnapshot = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    !string.Equals(snapshot["screenId"]?.GetValue<string>(), "main-menu", StringComparison.Ordinal) &&
                    string.Equals(snapshot["inputKind"]?.GetValue<string>(), "text", StringComparison.Ordinal));

            Assert.Equal("textPrompt", inGameSnapshot["mode"]!.GetValue<string>());
            Assert.True(inGameSnapshot["awaitingInput"]!.GetValue<bool>());
            Assert.Contains("Ваш ход", inGameSnapshot["title"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/статус", inGameSnapshot["plainText"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
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

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stdout.txt"), stdout);
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stderr.txt"), stderr);
        }
    }

    [Fact]
    public async Task AgentConsoleLiveControl_EndOfLifePublishesConfirmationAndAcceptsYesAction()
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

            await ContinueToGameLoopAsync(client);

            using var endLifeResponse = await client.PostAsJsonAsync("/api/agent-console/text", new { text = "/конец_жизни" });
            Assert.True(
                endLifeResponse.IsSuccessStatusCode,
                $"Expected /конец_жизни endpoint success, got {(int)endLifeResponse.StatusCode}: {await endLifeResponse.Content.ReadAsStringAsync()}");

            var confirmation = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(10),
                snapshot =>
                    string.Equals(snapshot["mode"]?.GetValue<string>(), "confirmation", StringComparison.Ordinal) &&
                    string.Equals(snapshot["inputKind"]?.GetValue<string>(), "confirmation", StringComparison.Ordinal));

            Assert.Equal("end-of-life-confirmation", confirmation["screenId"]!.GetValue<string>());
            Assert.True(confirmation["awaitingInput"]!.GetValue<bool>());
            Assert.Contains("завершить смертную жизнь", confirmation["plainText"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            var yesAction = confirmation["actions"]!.AsArray()[0]!.AsObject();
            var noAction = confirmation["actions"]!.AsArray()[1]!.AsObject();
            Assert.Equal("Да", yesAction["label"]!.GetValue<string>());
            Assert.Equal("y", yesAction["shortcut"]!.GetValue<string>());
            Assert.Equal("Нет", noAction["label"]!.GetValue<string>());
            Assert.Equal("n", noAction["shortcut"]!.GetValue<string>());
            Assert.True(noAction["isDefault"]!.GetValue<bool>());

            using var yesResponse = await client.PostAsJsonAsync("/api/agent-console/action", new
            {
                actionId = yesAction["id"]!.GetValue<string>(),
                screenId = confirmation["screenId"]!.GetValue<string>(),
                inputKind = "confirmation"
            });
            Assert.True(
                yesResponse.IsSuccessStatusCode,
                $"Expected Yes action endpoint success, got {(int)yesResponse.StatusCode}: {await yesResponse.Content.ReadAsStringAsync()}");

            var summaryPrompt = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(10),
                snapshot =>
                    string.Equals(snapshot["screenId"]?.GetValue<string>(), "end-of-life-summary", StringComparison.Ordinal) &&
                    string.Equals(snapshot["inputKind"]?.GetValue<string>(), "text", StringComparison.Ordinal));

            Assert.Contains("Итоги смертной жизни", summaryPrompt["plainText"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
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

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stdout.txt"), stdout);
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stderr.txt"), stderr);
        }
    }

    [Fact]
    public async Task AgentConsoleLiveControl_CommandResultPublishesWaitKeySnapshot()
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

            var mainMenu = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(30),
                snapshot => string.Equals(snapshot["screenId"]?.GetValue<string>(), "main-menu", StringComparison.Ordinal));

            var continueAction = mainMenu["actions"]!.AsArray()[0]!.AsObject();
            using var continueResponse = await client.PostAsJsonAsync("/api/agent-console/action", new
            {
                actionId = continueAction["id"]!.GetValue<string>(),
                screenId = mainMenu["screenId"]!.GetValue<string>(),
                inputKind = "menuSelection"
            });
            Assert.True(
                continueResponse.IsSuccessStatusCode,
                $"Expected Continue action endpoint success, got {(int)continueResponse.StatusCode}: {await continueResponse.Content.ReadAsStringAsync()}");

            var inGameSnapshot = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    string.Equals(snapshot["screenId"]?.GetValue<string>(), "game-loop", StringComparison.Ordinal) &&
                    string.Equals(snapshot["inputKind"]?.GetValue<string>(), "text", StringComparison.Ordinal));

            using var commandResponse = await client.PostAsJsonAsync("/api/agent-console/text", new { text = "/статус" });
            Assert.True(
                commandResponse.IsSuccessStatusCode,
                $"Expected text endpoint success, got {(int)commandResponse.StatusCode}: {await commandResponse.Content.ReadAsStringAsync()}");

            var commandSnapshot = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    !string.Equals(snapshot["screenId"]?.GetValue<string>(), inGameSnapshot["screenId"]?.GetValue<string>(), StringComparison.Ordinal) &&
                    string.Equals(snapshot["inputKind"]?.GetValue<string>(), "key", StringComparison.Ordinal));

            Assert.Equal("textPrompt", commandSnapshot["mode"]!.GetValue<string>());
            Assert.True(commandSnapshot["awaitingInput"]!.GetValue<bool>());
            Assert.Contains("Статус персонажа", commandSnapshot["plainText"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Здоровье", commandSnapshot["plainText"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Нажмите любую клавишу", commandSnapshot["plainText"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

            using var keyResponse = await client.PostAsJsonAsync("/api/agent-console/key", new { key = "Enter" });
            Assert.True(
                keyResponse.IsSuccessStatusCode,
                $"Expected key endpoint success, got {(int)keyResponse.StatusCode}: {await keyResponse.Content.ReadAsStringAsync()}");

            var commandUpdatedAt = commandSnapshot["updatedAtUtc"]!.GetValue<DateTimeOffset>();
            var returnedPrompt = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    string.Equals(snapshot["screenId"]?.GetValue<string>(), "game-loop", StringComparison.Ordinal) &&
                    string.Equals(snapshot["inputKind"]?.GetValue<string>(), "text", StringComparison.Ordinal) &&
                    snapshot["updatedAtUtc"]?.GetValue<DateTimeOffset>() > commandUpdatedAt);

            Assert.Contains("/статус", returnedPrompt["plainText"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
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

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stdout.txt"), stdout);
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stderr.txt"), stderr);
        }
    }

    [Fact]
    public async Task AgentConsoleLiveControl_CommandCaptureDoesNotLeakAndSelectionPromptsDoNotThrow()
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

            var gameLoop = await ContinueToGameLoopAsync(client);

            using var inventoryResponse = await client.PostAsJsonAsync("/api/agent-console/text", new { text = "/инв" });
            Assert.True(
                inventoryResponse.IsSuccessStatusCode,
                $"Expected /инв endpoint success, got {(int)inventoryResponse.StatusCode}: {await inventoryResponse.Content.ReadAsStringAsync()}");

            var inventorySnapshot = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    !string.Equals(snapshot["screenId"]?.GetValue<string>(), gameLoop["screenId"]?.GetValue<string>(), StringComparison.Ordinal) &&
                    IsCommandResultInput(snapshot));

            var inventoryText = inventorySnapshot["plainText"]!.GetValue<string>();
            Assert.Contains("Инвентарь", inventoryText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Cannot show selection prompt", inventoryText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NotSupportedException", inventoryText, StringComparison.OrdinalIgnoreCase);

            await CloseCommandResultAsync(client, inventorySnapshot);

            using var questsResponse = await client.PostAsJsonAsync("/api/agent-console/text", new { text = "/квесты" });
            Assert.True(
                questsResponse.IsSuccessStatusCode,
                $"Expected /квесты endpoint success, got {(int)questsResponse.StatusCode}: {await questsResponse.Content.ReadAsStringAsync()}");

            var questsSnapshot = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    !string.Equals(snapshot["screenId"]?.GetValue<string>(), inventorySnapshot["screenId"]?.GetValue<string>(), StringComparison.Ordinal) &&
                    IsCommandResultInput(snapshot));

            var questsText = questsSnapshot["plainText"]!.GetValue<string>();
            Assert.DoesNotContain("Cannot show selection prompt", questsText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NotSupportedException", questsText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Инвентарь пуст", questsText, StringComparison.OrdinalIgnoreCase);

            await CloseCommandResultAsync(client, questsSnapshot);

            using var locationsResponse = await client.PostAsJsonAsync("/api/agent-console/text", new { text = "/локации" });
            Assert.True(
                locationsResponse.IsSuccessStatusCode,
                $"Expected /локации endpoint success, got {(int)locationsResponse.StatusCode}: {await locationsResponse.Content.ReadAsStringAsync()}");

            var locationsSnapshot = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    !string.Equals(snapshot["screenId"]?.GetValue<string>(), questsSnapshot["screenId"]?.GetValue<string>(), StringComparison.Ordinal) &&
                    IsCommandResultInput(snapshot));

            var locationsText = locationsSnapshot["plainText"]!.GetValue<string>();
            Assert.Contains("Локации", locationsText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Cannot show selection prompt", locationsText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NotSupportedException", locationsText, StringComparison.OrdinalIgnoreCase);
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

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stdout.txt"), stdout);
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stderr.txt"), stderr);
        }
    }

    [Fact]
    public async Task AgentConsoleLiveControl_CommandDrilldownSelectionPromptPublishesMenuSnapshot()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureGameSessionPath = Path.Combine(repoRoot, "FileSystemExample", "game_session");
        using var sandbox = ConsoleE2ESandbox.CreateFromFixture(
            fixtureGameSessionPath,
            _tempRoot,
            preserveArtifacts: true);
        SeedAcceptedReadableDocument(sandbox.GameSessionPath);

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

            var gameLoop = await ContinueToGameLoopAsync(client);

            using var booksResponse = await client.PostAsJsonAsync("/api/agent-console/text", new { text = "/книги" });
            Assert.True(
                booksResponse.IsSuccessStatusCode,
                $"Expected /книги endpoint success, got {(int)booksResponse.StatusCode}: {await booksResponse.Content.ReadAsStringAsync()}");

            var booksMenu = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    !string.Equals(snapshot["screenId"]?.GetValue<string>(), gameLoop["screenId"]?.GetValue<string>(), StringComparison.Ordinal) &&
                    string.Equals(snapshot["mode"]?.GetValue<string>(), "menu", StringComparison.Ordinal) &&
                    string.Equals(snapshot["inputKind"]?.GetValue<string>(), "menuSelection", StringComparison.Ordinal));

            Assert.True(booksMenu["awaitingInput"]!.GetValue<bool>());
            Assert.Contains("кни", booksMenu["plainText"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            Assert.True(booksMenu["actions"]!.AsArray().Count > 0);

            var closeAction = FindActionByLabel(booksMenu, "Закрыть", "Назад", "Back", "Close");
            using var closeResponse = await client.PostAsJsonAsync("/api/agent-console/action", new
            {
                actionId = closeAction.Action["id"]!.GetValue<string>(),
                screenId = booksMenu["screenId"]!.GetValue<string>(),
                inputKind = "menuSelection"
            });
            Assert.True(
                closeResponse.IsSuccessStatusCode,
                $"Expected command drilldown close action success, got {(int)closeResponse.StatusCode}: {await closeResponse.Content.ReadAsStringAsync()}");

            var afterClose = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    string.Equals(snapshot["screenId"]?.GetValue<string>(), "game-loop", StringComparison.Ordinal) ||
                    string.Equals(snapshot["inputKind"]?.GetValue<string>(), "key", StringComparison.Ordinal));

            if (string.Equals(afterClose["inputKind"]?.GetValue<string>(), "key", StringComparison.Ordinal))
            {
                using var continueResponse = await client.PostAsJsonAsync("/api/agent-console/key", new { key = "Enter" });
                Assert.True(
                    continueResponse.IsSuccessStatusCode,
                    $"Expected command-result continue key success, got {(int)continueResponse.StatusCode}: {await continueResponse.Content.ReadAsStringAsync()}");
            }

            await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    string.Equals(snapshot["screenId"]?.GetValue<string>(), "game-loop", StringComparison.Ordinal) &&
                    string.Equals(snapshot["inputKind"]?.GetValue<string>(), "text", StringComparison.Ordinal));
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

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stdout.txt"), stdout);
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stderr.txt"), stderr);
        }
    }

    [Fact]
    public async Task AgentConsoleLiveControl_InGameOptionsCommandPublishesMenuSnapshotAndCloses()
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

            var gameLoop = await ContinueToGameLoopAsync(client);

            using var optionsResponse = await client.PostAsJsonAsync("/api/agent-console/text", new { text = "/опции" });
            Assert.True(
                optionsResponse.IsSuccessStatusCode,
                $"Expected /опции endpoint success, got {(int)optionsResponse.StatusCode}: {await optionsResponse.Content.ReadAsStringAsync()}");

            var optionsMenu = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    !string.Equals(snapshot["screenId"]?.GetValue<string>(), gameLoop["screenId"]?.GetValue<string>(), StringComparison.Ordinal) &&
                    string.Equals(snapshot["mode"]?.GetValue<string>(), "menu", StringComparison.Ordinal) &&
                    string.Equals(snapshot["inputKind"]?.GetValue<string>(), "menuSelection", StringComparison.Ordinal));

            Assert.True(optionsMenu["awaitingInput"]!.GetValue<bool>());
            Assert.Contains("Игровое меню", optionsMenu["title"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            Assert.True(optionsMenu["actions"]!.AsArray().Count >= 4);
            Assert.Contains(
                optionsMenu["actions"]!.AsArray(),
                action => action!["label"]!.GetValue<string>().Contains("Опции", StringComparison.OrdinalIgnoreCase));

            var backAction = FindActionByLabel(optionsMenu, "Назад", "Back");
            using var backResponse = await client.PostAsJsonAsync("/api/agent-console/action", new
            {
                actionId = backAction.Action["id"]!.GetValue<string>(),
                screenId = optionsMenu["screenId"]!.GetValue<string>(),
                inputKind = "menuSelection"
            });
            Assert.True(
                backResponse.IsSuccessStatusCode,
                $"Expected options back action success, got {(int)backResponse.StatusCode}: {await backResponse.Content.ReadAsStringAsync()}");

            await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    string.Equals(snapshot["screenId"]?.GetValue<string>(), "game-loop", StringComparison.Ordinal) &&
                    string.Equals(snapshot["inputKind"]?.GetValue<string>(), "text", StringComparison.Ordinal));
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

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stdout.txt"), stdout);
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stderr.txt"), stderr);
        }
    }

    [Fact]
    public async Task AgentConsoleLiveControl_QtePracticeMenuPublishesSnapshotAndDoesNotUseSpectrePrompt()
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

            var mainMenu = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(30),
                snapshot => string.Equals(snapshot["screenId"]?.GetValue<string>(), "main-menu", StringComparison.Ordinal));

            var qtePracticeAction = FindActionByLabel(mainMenu, "Тренировка QTE");
            using var enterQteResponse = await client.PostAsJsonAsync("/api/agent-console/action", new
            {
                actionId = qtePracticeAction.Action["id"]!.GetValue<string>(),
                screenId = mainMenu["screenId"]!.GetValue<string>(),
                inputKind = "menuSelection"
            });
            Assert.True(
                enterQteResponse.IsSuccessStatusCode,
                $"Expected QTE practice enter action endpoint success, got {(int)enterQteResponse.StatusCode}: {await enterQteResponse.Content.ReadAsStringAsync()}");

            var qteSnapshot = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                snapshot =>
                    string.Equals(snapshot["screenId"]?.GetValue<string>(), "qte-practice-type", StringComparison.Ordinal) &&
                    string.Equals(snapshot["inputKind"]?.GetValue<string>(), "menuSelection", StringComparison.Ordinal));

            var qteText = qteSnapshot["plainText"]!.GetValue<string>();
            Assert.Contains("Свободная тренировка QTE", qteText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Полоса реакции", qteText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Cannot show selection prompt", qteText, StringComparison.OrdinalIgnoreCase);
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

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stdout.txt"), stdout);
            File.WriteAllText(Path.Combine(sandbox.BasePath, "stderr.txt"), stderr);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static void SeedAcceptedReadableDocument(string gameSessionPath)
    {
        const string itemId = "itm_agent_console_readable_document";
        var document = MortalItemTestFixture.CreateCanonicalRoot(itemId);
        document["name"] = "Путевой журнал";
        document["description"] = "Журнал для проверки меню чтения в Agent Console.";
        document["type"] = "Книга";
        document["group"] = "Документы и медиа";
        document["textContent"] = new JsonArray(
            "На первой странице отмечен безопасный путь к северным воротам.");
        document["materialization"]!["sections"]!["readableOrSentient"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        MortalItemTestFixture.ResealCanonical(document);

        var inventoryPath = Path.Combine(gameSessionPath, "game_state", "inventory", "items.json");
        var inventory = JsonNode.Parse(File.ReadAllText(inventoryPath))?.AsObject()
                        ?? throw new InvalidDataException("Agent Console E2E inventory fixture must be a JSON object.");
        var items = inventory["items"]?.AsArray()
                    ?? throw new InvalidDataException("Agent Console E2E inventory fixture must contain items[].");
        items.Add(document);
        File.WriteAllText(
            inventoryPath,
            inventory.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var indexPath = Path.Combine(gameSessionPath, "game_state", "inventory", "item_identity_index.json");
        var index = JsonNode.Parse(File.ReadAllText(indexPath))?.AsObject()
                    ?? throw new InvalidDataException("Agent Console E2E item identity fixture must be a JSON object.");
        var entries = index["entries"]?.AsArray()
                      ?? throw new InvalidDataException("Agent Console E2E item identity fixture must contain entries[].");
        var documentIndex = MortalItemTestFixture.CreateIndexForCarrier(
            document,
            "player_inventory",
            "player");
        entries.Add(documentIndex["entries"]![0]!.DeepClone());
        File.WriteAllText(
            indexPath,
            index.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
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

    private static bool IsCommandResultInput(JsonObject snapshot)
    {
        var inputKind = snapshot["inputKind"]?.GetValue<string>();
        return string.Equals(inputKind, "key", StringComparison.Ordinal) ||
               string.Equals(inputKind, "menuSelection", StringComparison.Ordinal);
    }

    private static async Task CloseCommandResultAsync(HttpClient client, JsonObject snapshot)
    {
        var inputKind = snapshot["inputKind"]?.GetValue<string>();
        if (string.Equals(inputKind, "key", StringComparison.Ordinal))
        {
            using var continueResponse = await client.PostAsJsonAsync("/api/agent-console/key", new { key = "Enter" });
            Assert.True(
                continueResponse.IsSuccessStatusCode,
                $"Expected command-result continue key success, got {(int)continueResponse.StatusCode}: {await continueResponse.Content.ReadAsStringAsync()}");
        }
        else if (string.Equals(inputKind, "menuSelection", StringComparison.Ordinal))
        {
            await ActivateMenuActionAsync(client, snapshot, "Назад", "Закрыть", "Back", "Close");
        }
        else
        {
            Assert.Fail($"Expected key or menuSelection command result, got '{inputKind ?? "<none>"}'.");
        }

        var afterClose = await WaitForSnapshotAsync(
            client,
            TimeSpan.FromSeconds(20),
            candidate =>
                string.Equals(candidate["screenId"]?.GetValue<string>(), "game-loop", StringComparison.Ordinal) ||
                string.Equals(candidate["inputKind"]?.GetValue<string>(), "key", StringComparison.Ordinal) ||
                string.Equals(candidate["screenId"]?.GetValue<string>(), snapshot["screenId"]?.GetValue<string>(), StringComparison.Ordinal));

        if (string.Equals(afterClose["screenId"]?.GetValue<string>(), snapshot["screenId"]?.GetValue<string>(), StringComparison.Ordinal) &&
            string.Equals(afterClose["inputKind"]?.GetValue<string>(), "menuSelection", StringComparison.Ordinal))
        {
            await ActivateMenuActionAsync(client, afterClose, "Назад", "Закрыть", "Back", "Close");
            afterClose = await WaitForSnapshotAsync(
                client,
                TimeSpan.FromSeconds(20),
                candidate =>
                    string.Equals(candidate["screenId"]?.GetValue<string>(), "game-loop", StringComparison.Ordinal) ||
                    string.Equals(candidate["inputKind"]?.GetValue<string>(), "key", StringComparison.Ordinal));
        }

        if (string.Equals(afterClose["inputKind"]?.GetValue<string>(), "key", StringComparison.Ordinal))
        {
            using var continueResponse = await client.PostAsJsonAsync("/api/agent-console/key", new { key = "Enter" });
            Assert.True(
                continueResponse.IsSuccessStatusCode,
                $"Expected command-result continue key success, got {(int)continueResponse.StatusCode}: {await continueResponse.Content.ReadAsStringAsync()}");
        }

        await WaitForSnapshotAsync(
            client,
            TimeSpan.FromSeconds(20),
            candidate =>
                string.Equals(candidate["screenId"]?.GetValue<string>(), "game-loop", StringComparison.Ordinal) &&
                string.Equals(candidate["inputKind"]?.GetValue<string>(), "text", StringComparison.Ordinal));
    }

    private static async Task ActivateMenuActionAsync(HttpClient client, JsonObject snapshot, params string[] labels)
    {
        var action = FindActionByLabel(snapshot, labels).Action;
        using var response = await client.PostAsJsonAsync("/api/agent-console/action", new
        {
            actionId = action["id"]!.GetValue<string>(),
            screenId = snapshot["screenId"]!.GetValue<string>(),
            inputKind = "menuSelection"
        });
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected menu action success, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<JsonObject> ContinueToGameLoopAsync(HttpClient client)
    {
        var mainMenu = await WaitForSnapshotAsync(
            client,
            TimeSpan.FromSeconds(30),
            snapshot => string.Equals(snapshot["screenId"]?.GetValue<string>(), "main-menu", StringComparison.Ordinal));

        var continueAction = mainMenu["actions"]!.AsArray()[0]!.AsObject();
        using var continueResponse = await client.PostAsJsonAsync("/api/agent-console/action", new
        {
            actionId = continueAction["id"]!.GetValue<string>(),
            screenId = mainMenu["screenId"]!.GetValue<string>(),
            inputKind = "menuSelection"
        });
        Assert.True(
            continueResponse.IsSuccessStatusCode,
            $"Expected Continue action endpoint success, got {(int)continueResponse.StatusCode}: {await continueResponse.Content.ReadAsStringAsync()}");

        return await WaitForSnapshotAsync(
            client,
            TimeSpan.FromSeconds(20),
            snapshot =>
                string.Equals(snapshot["screenId"]?.GetValue<string>(), "game-loop", StringComparison.Ordinal) &&
                string.Equals(snapshot["inputKind"]?.GetValue<string>(), "text", StringComparison.Ordinal));
    }

    private static (JsonObject Action, int Index) FindExitAction(JsonObject snapshot)
        => FindActionByLabel(snapshot, "Exit", "Выход");

    private static (JsonObject Action, int Index) FindActionByLabel(JsonObject snapshot, params string[] labels)
    {
        var actions = snapshot["actions"]!.AsArray();
        for (var index = 0; index < actions.Count; index++)
        {
            var action = actions[index]!.AsObject();
            var label = action["label"]!.GetValue<string>();
            if (labels.Any(expected => label.Contains(expected, StringComparison.OrdinalIgnoreCase)))
            {
                return (action, index);
            }
        }

        Assert.Fail($"Expected snapshot to expose an action containing one of: {string.Join(", ", labels)}.");
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
