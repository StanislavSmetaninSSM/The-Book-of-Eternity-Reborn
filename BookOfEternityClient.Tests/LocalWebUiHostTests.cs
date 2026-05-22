using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LocalWebUiHostTests : IDisposable
{
    private readonly string _rootPath;

    public LocalWebUiHostTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-local-web-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void Build_RejectsNonLoopbackUrls()
    {
        var options = new LocalWebUiHostOptions(_rootPath, "http://0.0.0.0:8787");

        var ex = Assert.Throws<InvalidOperationException>(() => LocalWebUiHost.Build(Array.Empty<string>(), options));

        Assert.Contains("localhost", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsLocalSessionStatus()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var json = await client.GetStringAsync("/api/health");
        var root = JsonNode.Parse(json)!.AsObject();

        Assert.Equal("ok", root["status"]!.GetValue<string>());
        Assert.True(root["localOnly"]!.GetValue<bool>());
        Assert.Equal(_rootPath, root["basePath"]!.GetValue<string>());
        Assert.Equal(Path.Combine(_rootPath, "game_session"), root["gameSessionPath"]!.GetValue<string>());
        Assert.True(root["canStartBrowserWrite"]!.GetValue<bool>());
        Assert.False(root["pendingTurn"]!["hasActiveGmTurn"]!.GetValue<bool>());
        Assert.False(root["localUiLock"]!["exists"]!.GetValue<bool>());
    }

    [Fact]
    public async Task SessionEndpoint_ReportsPendingTurnAndLocalUiLock()
    {
        WriteSessionFile("input/turn_request.json", "{}");
        WriteSessionFile("game_state/control/local_ui_session_lock.json", """
        {
          "schemaVersion": 1,
          "ownerId": "console-owner",
          "ownerKind": "console",
          "ownerLabel": "Console",
          "acquiredAtUtc": "2026-05-21T00:00:00.0000000Z",
          "heartbeatAtUtc": "2026-05-21T00:00:00.0000000Z",
          "leaseSeconds": 120,
          "lastOperation": "console write"
        }
        """);
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var json = await client.GetStringAsync("/api/session");
        var root = JsonNode.Parse(json)!.AsObject();

        Assert.False(root["canStartBrowserWrite"]!.GetValue<bool>());
        Assert.True(root["pendingTurn"]!["hasActiveGmTurn"]!.GetValue<bool>());
        Assert.Contains(root["pendingTurn"]!["artifacts"]!.AsArray(), node =>
            string.Equals(node?["path"]?.GetValue<string>(), "input/turn_request.json", StringComparison.OrdinalIgnoreCase) &&
            node?["exists"]?.GetValue<bool>() == true);
        Assert.True(root["localUiLock"]!["exists"]!.GetValue<bool>());
        Assert.Equal("console-owner", root["localUiLock"]!["ownerId"]!.GetValue<string>());
    }

    [Fact]
    public async Task RootEndpoint_ReturnsBrowserShellHtml()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");

        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The Book of Eternity", html, StringComparison.Ordinal);
        Assert.Contains("/api/health", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RootEndpoint_IncludesCommandRendererAssets()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");

        Assert.Contains("id=\"command-form\"", html, StringComparison.Ordinal);
        Assert.Contains("renderCommandResult", html, StringComparison.Ordinal);
        Assert.Contains("renderBlock", html, StringComparison.Ordinal);
        Assert.Contains("POST", html, StringComparison.Ordinal);
        Assert.Contains("/api/explorer/command", html, StringComparison.Ordinal);
        Assert.Contains("renderNotifications", html, StringComparison.Ordinal);
        Assert.Contains("action.command", html, StringComparison.Ordinal);
        Assert.Contains("prompt.prompt", html, StringComparison.Ordinal);
        Assert.Contains("/api/qte/state", html, StringComparison.Ordinal);
        Assert.Contains("renderQteState", html, StringComparison.Ordinal);
        Assert.Contains("postQteAction", html, StringComparison.Ordinal);
        Assert.Contains("renderImageBlock", html, StringComparison.Ordinal);
        Assert.Contains("renderMapBlock", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/assets/map-viewer.css\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"/assets/map-viewer.js\"", html, StringComparison.Ordinal);
        Assert.Contains("/api/media/", html, StringComparison.Ordinal);
        Assert.Contains("Пока нет результата", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MapViewerAssetEndpoints_ReturnSharedRendererPackage()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var css = await client.GetStringAsync("/assets/map-viewer.css");
        var js = await client.GetStringAsync("/assets/map-viewer.js");

        Assert.Equal(LocalMapViewerAssets.StyleSheet, css);
        Assert.Equal(LocalMapViewerAssets.Script, js);
        Assert.Contains(".map-block", css, StringComparison.Ordinal);
        Assert.Contains("BookOfEternityMapViewer", js, StringComparison.Ordinal);
        Assert.Contains("renderMapBlock", js, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RootEndpoint_UsesSharedMapViewerPackage()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");

        Assert.Contains("href=\"/assets/map-viewer.css\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"/assets/map-viewer.js\"", html, StringComparison.Ordinal);
        Assert.Contains("BookOfEternityMapViewer.renderMapBlock", html, StringComparison.Ordinal);
        Assert.DoesNotContain("function renderMapBlock(block)", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RootEndpoint_IncludesFullGameShellNavigation()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");

        Assert.Contains("id=\"command-palette-filter\"", html, StringComparison.Ordinal);
        Assert.Contains("data-command=\"/quests\"", html, StringComparison.Ordinal);
        Assert.Contains("data-command=\"/chaos_sea\"", html, StringComparison.Ordinal);
        Assert.Contains("data-command=\"/shining_abode\"", html, StringComparison.Ordinal);
        Assert.Contains("data-command=\"/spiritual_conflict\"", html, StringComparison.Ordinal);
        Assert.Contains("data-command=\"/afterlife_archive\"", html, StringComparison.Ordinal);
        Assert.Contains("data-command=\"/validate\"", html, StringComparison.Ordinal);
        Assert.Contains("Мир смертных", html, StringComparison.Ordinal);
        Assert.Contains("Море Хаоса", html, StringComparison.Ordinal);
        Assert.Contains("Сияющая Обитель", html, StringComparison.Ordinal);
        Assert.Contains("Духовный бой", html, StringComparison.Ordinal);
        Assert.Contains("История и архив", html, StringComparison.Ordinal);
        Assert.Contains("Диагностика", html, StringComparison.Ordinal);
        Assert.Contains("filterCommandPalette", html, StringComparison.Ordinal);
        Assert.Contains("renderProgressState", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MediaEndpoint_ReturnsApprovedImageFile()
    {
        WriteSessionImage("images/npcs/hero.png");
        var mediaId = LocalMediaService.CreateMediaIdForRelativePath("images/npcs/hero.png");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var response = await client.GetAsync("/api/media/" + Uri.EscapeDataString(mediaId));

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 0);
    }

    [Fact]
    public async Task MediaEndpoint_RejectsTraversalOutsideApprovedRoots()
    {
        WriteSessionFile("game_state/meta/soul_state.png", "not an image root");
        var mediaId = LocalMediaService.CreateMediaIdForRelativePath("game_state/meta/soul_state.png");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var response = await client.GetAsync("/api/media/" + Uri.EscapeDataString(mediaId));
        var json = JsonNode.Parse((await response.Content.ReadAsStringAsync())!)!.AsObject();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("разреш", json["error"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RootEndpoint_IncludesLifecycleDashboardAssets()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");

        Assert.Contains("id=\"lifecycle-panel\"", html, StringComparison.Ordinal);
        Assert.Contains("Панель состояния", html, StringComparison.Ordinal);
        Assert.Contains("Проверить валидацию", html, StringComparison.Ordinal);
        Assert.Contains("/api/lifecycle/dashboard", html, StringComparison.Ordinal);
        Assert.Contains("/api/lifecycle/validate", html, StringComparison.Ordinal);
        Assert.Contains("renderLifecycleDashboard", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LifecycleDashboardEndpoint_ReturnsSessionRealmPendingAndValidationSummary()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "Web Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 9
        }
        """);
        WriteSessionFile("input/turn_request.json", "{}");

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var json = await client.GetStringAsync("/api/lifecycle/dashboard");
        var root = JsonNode.Parse(json)!.AsObject();

        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal(_rootPath, root["session"]!["basePath"]!.GetValue<string>());
        Assert.Equal(Path.Combine(_rootPath, "game_session"), root["session"]!["gameSessionPath"]!.GetValue<string>());
        Assert.Equal("Web Soul", root["soul"]!["name"]!.GetValue<string>());
        Assert.Equal("Chaos Sea", root["soul"]!["currentRealm"]!.GetValue<string>());
        Assert.Equal(9, root["soul"]!["currentIncarnation"]!.GetValue<int>());
        Assert.True(root["pendingTurn"]!["hasActiveGmTurn"]!.GetValue<bool>());
        Assert.True(root["validation"]!["issueCount"]!.GetValue<int>() >= 0);
        Assert.Contains(root["guidance"]!.AsArray(), node =>
            node?["title"]?.GetValue<string>().Contains("Ход ГМа", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task LifecycleValidateEndpoint_ReturnsGroupedValidationIssues()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var response = await client.PostAsJsonAsync("/api/lifecycle/validate", new { });
        var root = JsonNode.Parse((await response.Content.ReadAsStringAsync())!)!.AsObject();

        response.EnsureSuccessStatusCode();
        Assert.True(root["issueCount"]!.GetValue<int>() > 0);
        Assert.True(root["errorCount"]!.GetValue<int>() > 0);
        Assert.NotEmpty(root["groups"]!.AsArray());
        Assert.NotEmpty(root["issues"]!.AsArray());
        Assert.Contains(root["issues"]!.AsArray(), node =>
            !string.IsNullOrWhiteSpace(node?["filePath"]?.GetValue<string>()) &&
            !string.IsNullOrWhiteSpace(node["message"]?.GetValue<string>()));
    }

    [Fact]
    public async Task ExplorerCommandEndpoint_ReturnsMigratedHelpDto()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var response = await client.PostAsJsonAsync("/api/explorer/command", new { command = "/help" });
        var json = await response.Content.ReadAsStringAsync();
        var root = JsonNode.Parse(json)!.AsObject();

        response.EnsureSuccessStatusCode();
        Assert.Equal("/help", root["command"]!.GetValue<string>());
        Assert.Equal("Completed", root["state"]!.GetValue<string>());
        Assert.Equal("table", root["blocks"]![0]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public async Task ExplorerCommandEndpoint_MatchesDirectWebCommandDtoSerialization()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "Web Soul",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 9,
          "inkFeathers": { "current": 15, "total": 25 },
          "enlightenment": { "currentTier": "Тлеющий знак", "experience": 80 }
        }
        """);

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var response = await client.PostAsJsonAsync("/api/explorer/command", new { command = "/status" });
        var actual = JsonNode.Parse((await response.Content.ReadAsStringAsync())!)!;

        var fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        var stateManager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var service = new ExplorerWebCommandService(
            fs,
            stateManager,
            new LocalizationManager(),
            new ValidationService(fs, NullLogger<ValidationService>.Instance));
        var expected = JsonSerializer.SerializeToNode(
            await service.ExecuteAsync(new ExplorerWebCommandRequest("/status")),
            JsonOptions)!;

        response.EnsureSuccessStatusCode();
        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            "/api/explorer/command must serialize the same logical DTO produced by ExplorerWebCommandService.");
    }

    [Fact]
    public async Task ExplorerCommandEndpoint_ReturnsLocalTurnProtocolForMutatingCommands()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var response = await client.PostAsJsonAsync("/api/explorer/command", new { command = "/spiritual_action" });
        var json = await response.Content.ReadAsStringAsync();
        var root = JsonNode.Parse(json)!.AsObject();

        response.EnsureSuccessStatusCode();
        Assert.Equal("/spiritual_action", root["command"]!.GetValue<string>());
        Assert.Equal("RequiresInput", root["state"]!.GetValue<string>());
        Assert.Equal("panel", root["blocks"]![0]!["kind"]!.GetValue<string>());
        Assert.Contains("Локальный ход", root["blocks"]![0]!["title"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(root["prompts"]!.AsArray());
    }

    [Fact]
    public async Task PromptSessionEndpoints_SubmitBrowserPromptAnswers()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var startResponse = await client.PostAsJsonAsync("/api/explorer/command", new
        {
            command = "/world_setup",
            ownerId = "browser-host-test",
            ownerLabel = "Browser host test"
        });
        var startRoot = JsonNode.Parse((await startResponse.Content.ReadAsStringAsync())!)!.AsObject();
        startResponse.EnsureSuccessStatusCode();
        var sessionId = startRoot["interactiveSession"]!["sessionId"]!.GetValue<string>();

        using var submitResponse = await client.PostAsJsonAsync("/api/explorer/prompt-sessions/submit", new
        {
            sessionId,
            ownerId = "browser-host-test",
            answers = new
            {
                world_setup_mode = "create_or_edit",
                world_title = "Пепельное королевство",
                world_directives = "Тёмное фэнтези, трагедия, родовые клятвы."
            }
        });
        var submitRoot = JsonNode.Parse((await submitResponse.Content.ReadAsStringAsync())!)!.AsObject();

        submitResponse.EnsureSuccessStatusCode();
        Assert.Equal("Completed", submitRoot["state"]!.GetValue<string>());
        Assert.Contains(submitRoot["blocks"]!.AsArray(), node =>
            string.Equals(node?["title"]?.GetValue<string>(), "Подготовка мира записана", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(_rootPath, "game_session", WorldDirectiveService.PendingSetupPath)));
        Assert.False(File.Exists(Path.Combine(_rootPath, "game_session", LocalUiSessionLockService.LockPath)));
    }

    [Fact]
    public async Task QteStateEndpoint_ReturnsPendingOffer()
    {
        WriteSessionFile("output/qte_offer.json", BuildSingleActionQteOfferJson());

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var json = await client.GetStringAsync("/api/qte/state");
        var root = JsonNode.Parse(json)!.AsObject();

        Assert.Equal("Offer", root["state"]!.GetValue<string>());
        Assert.Equal("qte_bridge", root["offer"]!["qteId"]!.GetValue<string>());
        Assert.Equal("Мост над бездной", root["offer"]!["title"]!.GetValue<string>());
        Assert.Contains(root["availableOperations"]!.AsArray(), node => node!.GetValue<string>() == "accept");
        Assert.Contains(root["availableOperations"]!.AsArray(), node => node!.GetValue<string>() == "decline");
    }

    [Fact]
    public async Task QteEndpoints_AcceptOfferAndResolveBranchChoiceAction()
    {
        WriteSessionFile("output/qte_offer.json", BuildSingleActionQteOfferJson());
        WriteSessionFile("game_state/player/experience.json", """
        {
          "totalExperience": 10
        }
        """);
        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая душа",
          "currentIncarnation": 0,
          "currentRealm": "Chaos Sea"
        }
        """);
        WriteSessionFile("game_state/meta/abode_power_journal.json", """
        {
          "entries": []
        }
        """);

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };

        using var acceptResponse = await client.PostAsJsonAsync("/api/qte/offer", new { decision = "accept" });
        var acceptRoot = JsonNode.Parse((await acceptResponse.Content.ReadAsStringAsync())!)!.AsObject();

        acceptResponse.EnsureSuccessStatusCode();
        Assert.Equal("Active", acceptRoot["state"]!.GetValue<string>());
        Assert.Equal("start", acceptRoot["activeScene"]!["currentChapter"]!["chapterId"]!.GetValue<string>());

        using var actionResponse = await client.PostAsJsonAsync("/api/qte/action", new { actionId = "cross_bridge" });
        var actionRoot = JsonNode.Parse((await actionResponse.Content.ReadAsStringAsync())!)!.AsObject();

        actionResponse.EnsureSuccessStatusCode();
        Assert.Equal("Completed", actionRoot["state"]!.GetValue<string>());
        Assert.Equal("qte_bridge", actionRoot["completion"]!["qteId"]!.GetValue<string>());
        Assert.Equal("safe_crossing", actionRoot["completion"]!["outcomeId"]!.GetValue<string>());

        var runtimePath = Path.Combine(_rootPath, "game_session", "game_state", "control", "qte_runtime.json");
        var runtimeJson = await File.ReadAllTextAsync(runtimePath);
        Assert.DoesNotContain("activeScene", runtimeJson, StringComparison.Ordinal);

        var historyPath = Path.Combine(_rootPath, "game_session", "game_state", "history", "qte_history.json");
        var historyJson = await File.ReadAllTextAsync(historyPath);
        Assert.Contains("qte_bridge", historyJson, StringComparison.Ordinal);

        var experienceJson = await File.ReadAllTextAsync(Path.Combine(_rootPath, "game_session", "game_state", "player", "experience.json"));
        Assert.Contains("\"totalExperience\": 15", experienceJson, StringComparison.Ordinal);
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

    private void WriteSessionFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_rootPath, "game_session", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private void WriteSessionImage(string relativePath)
    {
        var fullPath = Path.Combine(_rootPath, "game_session", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, [137, 80, 78, 71, 13, 10, 26, 10]);
    }

    private static string BuildSingleActionQteOfferJson() =>
        """
        {
          "qteId": "qte_bridge",
          "title": "Мост над бездной",
          "offerText": "Перед вами рушится мост.",
          "introNarrative": "Камни уходят вниз, но ещё можно прыгнуть.",
          "declineHint": "Можно отказаться от QTE и оставить сцену обычной проверке.",
          "cinematicJustification": "Редкая кинематографичная сцена.",
          "startChapterId": "start",
          "chapters": [
            {
              "chapterId": "start",
              "title": "Прыжок",
              "narrative": "Вы выбираете момент для прыжка.",
              "actions": [
                {
                  "actionId": "cross_bridge",
                  "label": "Прыгнуть через провал",
                  "check": {
                    "type": "BranchChoice",
                    "baseDifficulty": 2,
                    "primaryCharacteristic": "dexterity",
                    "config": { "choiceGrade": "success" }
                  },
                  "routing": {
                    "success": { "terminalOutcomeId": "safe_crossing" },
                    "partial": { "terminalOutcomeId": "safe_crossing" },
                    "fail": { "terminalOutcomeId": "safe_crossing" }
                  },
                  "successText": "Вы перелетаете через провал."
                }
              ]
            }
          ],
          "terminalOutcomes": [
            {
              "outcomeId": "safe_crossing",
              "title": "Переход",
              "finalNarrative": "Вы выбрались на другую сторону.",
              "gmSummary": "Игрок прошёл QTE-мост.",
              "responseFragment": {
                "response": "Вы выбрались на другую сторону.",
                "experienceGained": 5
              }
            }
          ]
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
