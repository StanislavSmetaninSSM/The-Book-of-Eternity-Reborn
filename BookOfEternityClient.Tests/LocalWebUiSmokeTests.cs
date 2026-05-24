using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LocalWebUiSmokeTests : IDisposable
{
    private readonly string _rootPath;

    public LocalWebUiSmokeTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-local-web-ui-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task BrowserWebUiSmoke_CoversRootMenuSessionGameScreenLifecycleAndCommandFlow()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "Дымовая душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 7,
          "inkFeathers": { "current": 11 },
          "enlightenment": { "currentTier": "Тлеющий знак" }
        }
        """);
        WriteSessionFile("game_state/world/current_location.json", """
        {
          "name": "Причал между мирами"
        }
        """);
        WriteSessionFile("output/narrative_response.json", """
        {
          "response": "Туман расступается перед книгой."
        }
        """);

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var rootHtml = await client.GetStringAsync("/");
        var menu = JsonNode.Parse(await client.GetStringAsync("/api/main-menu"))!.AsObject();
        var session = JsonNode.Parse(await client.GetStringAsync("/api/session"))!.AsObject();
        var screen = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        var lifecycle = JsonNode.Parse(await client.GetStringAsync("/api/lifecycle/dashboard"))!.AsObject();
        using var commandResponse = await client.PostAsJsonAsync("/api/explorer/command", new { command = "/status" });
        var command = JsonNode.Parse(await commandResponse.Content.ReadAsStringAsync())!.AsObject();

        Assert.Contains("id=\"main-menu\"", rootHtml, StringComparison.Ordinal);
        Assert.Contains("data-menu-action=\"continue\"", rootHtml, StringComparison.Ordinal);
        Assert.Equal("Дымовая душа", menu["session"]!["soulName"]!.GetValue<string>());
        Assert.True(session["localOnly"]!.GetValue<bool>());
        Assert.Equal("Дымовая душа", screen["soul"]!["name"]!.GetValue<string>());
        Assert.Equal("Chaos Sea", screen["soul"]!["realm"]!.GetValue<string>());
        Assert.Equal(7, screen["soul"]!["incarnation"]!.GetValue<int>());
        Assert.Equal(11, screen["soul"]!["inkFeathers"]!.GetValue<int>());
        Assert.Equal("Причал между мирами", screen["world"]!["location"]!.GetValue<string>());
        Assert.Contains("Туман", screen["narrative"]!["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(screen["flags"]!["isInChaosSea"]!.GetValue<bool>());
        Assert.Equal(1, lifecycle["schemaVersion"]!.GetValue<int>());
        Assert.Equal("Дымовая душа", lifecycle["soul"]!["name"]!.GetValue<string>());
        Assert.Equal("Chaos Sea", lifecycle["soul"]!["currentRealm"]!.GetValue<string>());
        Assert.False(lifecycle["pendingTurn"]!["hasActiveGmTurn"]!.GetValue<bool>());
        Assert.True(lifecycle["validation"]!["issueCount"]!.GetValue<int>() >= 0);
        commandResponse.EnsureSuccessStatusCode();
        Assert.Equal("Completed", command["state"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task BrowserWebUiSmoke_SubmitsBrowserFormFlowWithoutConsolePrompts()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var startResponse = await client.PostAsJsonAsync("/api/explorer/command", new
        {
            command = "/world_setup",
            ownerId = "browser-smoke-test",
            ownerLabel = "Browser smoke test"
        });
        var startRoot = JsonNode.Parse(await startResponse.Content.ReadAsStringAsync())!.AsObject();
        startResponse.EnsureSuccessStatusCode();
        var sessionId = startRoot["interactiveSession"]!["sessionId"]!.GetValue<string>();

        using var submitResponse = await client.PostAsJsonAsync("/api/explorer/prompt-sessions/submit", new
        {
            sessionId,
            ownerId = "browser-smoke-test",
            answers = new
            {
                world_setup_mode = "create_or_edit",
                world_title = "Пепельное королевство",
                world_directives = "Тёмное фэнтези, трагедия, родовые клятвы."
            }
        });
        var submitRoot = JsonNode.Parse(await submitResponse.Content.ReadAsStringAsync())!.AsObject();

        submitResponse.EnsureSuccessStatusCode();
        Assert.Equal("Completed", submitRoot["state"]!.GetValue<string>());
        Assert.Contains(submitRoot["blocks"]!.AsArray(), node =>
            string.Equals(node?["title"]?.GetValue<string>(), "Подготовка мира записана", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(_rootPath, "game_session", WorldDirectiveService.PendingSetupPath)));
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task BrowserWebUiSmoke_PlayerDefaultIsRussianFirstAndKeepsTechnicalCopyOutOfPrimaryMenu()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");
        var advancedIndex = html.IndexOf("<section id=\"advanced-shell\"", StringComparison.Ordinal);
        Assert.True(advancedIndex > 0, "The smoke guard needs a default player fragment before the advanced panel.");
        var playerDefault = html[..advancedIndex];

        Assert.Contains("Продолжить", playerDefault, StringComparison.Ordinal);
        Assert.Contains("Новая игра", playerDefault, StringComparison.Ordinal);
        Assert.Contains("Загрузить", playerDefault, StringComparison.Ordinal);
        Assert.DoesNotContain("debug", playerDefault, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", playerDefault, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HTTP", playerDefault, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/", playerDefault, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
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

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
