using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using BookOfEternityClient.WebUi;
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
        Assert.Contains("Пока нет результата", html, StringComparison.Ordinal);
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
        var acceptRoot = JsonNode.Parse(await acceptResponse.Content.ReadAsStringAsync())!.AsObject();

        acceptResponse.EnsureSuccessStatusCode();
        Assert.Equal("Active", acceptRoot["state"]!.GetValue<string>());
        Assert.Equal("start", acceptRoot["activeScene"]!["currentChapter"]!["chapterId"]!.GetValue<string>());

        using var actionResponse = await client.PostAsJsonAsync("/api/qte/action", new { actionId = "cross_bridge" });
        var actionRoot = JsonNode.Parse(await actionResponse.Content.ReadAsStringAsync())!.AsObject();

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

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
