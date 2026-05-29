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
using Microsoft.Extensions.DependencyInjection;
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

    private static string FallbackFrontendRoot => Path.Combine(
        TestRepoPaths.RepoRoot,
        "BookOfEternityClient.WebFrontend",
        "public");

    private LocalWebUiHostOptions CreateHostOptions(string url) =>
        new(_rootPath, url, FallbackFrontendRoot);

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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task GameScreenEndpoint_ReturnsNoActiveSessionForFreshEmptyRoot()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var response = await client.GetAsync("/api/game-screen");
        var body = await response.Content.ReadAsStringAsync();
        var root = JsonNode.Parse(body)!.AsObject();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("game_session", root["error"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("актив", root["error"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("soul_state.json", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("валидац", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("repair", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public async Task CommandCoverageEndpoint_ReturnsMachineReadableExplorerParityMatrix()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var root = JsonNode.Parse(await client.GetStringAsync("/api/explorer/command-coverage"))!.AsObject();

        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.True(root["summary"]!["descriptorCount"]!.GetValue<int>() >= 1);
        var commands = root["commands"]!.AsArray();
        Assert.Contains(commands, node => node?["id"]?.GetValue<string>() == "saref_story");
        Assert.Contains(commands, node => node?["id"]?.GetValue<string>() == "validate" && node?["surface"]?.GetValue<string>() == "advanced-only");
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
    public async Task MainMenuEndpoint_ReturnsSessionActionsAndBrowserFriendlyDisabledStates()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "Веб-душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 4
        }
        """);
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var json = await client.GetStringAsync("/api/main-menu");
        var root = JsonNode.Parse(json)!.AsObject();
        var actions = root["actions"]!.AsArray();

        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.True(root["session"]!["canContinue"]!.GetValue<bool>());
        Assert.Equal("Веб-душа", root["session"]!["soulName"]!.GetValue<string>());
        Assert.Equal("Море Хаоса", root["session"]!["realmLabel"]!.GetValue<string>());
        Assert.Contains("Ход", root["session"]!["turnLabel"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(actions, action => action?["id"]?.GetValue<string>() == "continue" && action["enabled"]!.GetValue<bool>());
        Assert.Contains(actions, action => action?["id"]?.GetValue<string>() == "new-game" && action["enabled"]!.GetValue<bool>());
        Assert.Contains(actions, action => action?["id"]?.GetValue<string>() == "load" && action["enabled"]!.GetValue<bool>() == false &&
            action["disabledReason"]!.GetValue<string>().Contains("сохран", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action?["id"]?.GetValue<string>() == "options" && action["enabled"]!.GetValue<bool>());
        Assert.Contains(actions, action => action?["id"]?.GetValue<string>() == "about" && action["enabled"]!.GetValue<bool>());
        Assert.Contains(actions, action => action?["id"]?.GetValue<string>() == "exit" && action["enabled"]!.GetValue<bool>() == false);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task MainMenuEndpoint_ReadsTurnNumberFromJsonLinesStoryHistory()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "Меню-душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2
        }
        """);
        WriteSessionFile("stories/chaos_sea.jsonl", """
        {"turn":4,"realm":"Chaos Sea","player":"Ранний ход","narrative":"Начало"}
        {"turn":19,"realm":"Chaos Sea","player":"Последний ход","narrative":"Продолжение"}
        """);
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var root = JsonNode.Parse(await client.GetStringAsync("/api/main-menu"))!.AsObject();

        Assert.Equal(19, root["session"]!["turnNumber"]!.GetValue<int>());
        Assert.Equal("Ход 19", root["session"]!["turnLabel"]!.GetValue<string>());
    }

    [Fact]
    public async Task MainMenuEndpoint_BlocksContinueForTerminalSoulDissipation()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "Развеянная душа",
          "currentRealm": "Chaos Sea",
          "terminalGameOver": {
            "state": "soul_dispersed",
            "message": "Вы мертвы. Ваша душа окончательно развеяна. Загрузите последнее сохранение и попробуйте снова"
          }
        }
        """);
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var json = await client.GetStringAsync("/api/main-menu");
        var root = JsonNode.Parse(json)!.AsObject();

        Assert.False(root["session"]!["canContinue"]!.GetValue<bool>());
        Assert.Contains("продолжить нельзя", root["session"]!["continueReason"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(root["actions"]!.AsArray(), action => action?["id"]?.GetValue<string>() == "continue" &&
            action["enabled"]!.GetValue<bool>() == false &&
            action["disabledReason"]!.GetValue<string>().Contains("сохранение", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task ClientSettingsEndpoint_LoadsPlayerSafeSharedSettingsAndLocality()
    {
        WriteSessionFile("config.json", """
        {
          "language": "en",
          "difficulty": "hard",
          "showGmThoughts": true,
          "musicEnabled": false,
          "musicVolume": 27,
          "soundEnabled": true,
          "soundVolume": 81,
          "browserFontScalePercent": 115,
          "browserReducedMotion": true,
          "browserContrastFriendly": true,
          "gmBridgeEnabled": false,
          "openRouterApiKey": "secret-token-not-for-browser",
          "gmCliLaunchCommand": "secret-shell-command"
        }
        """);
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var root = JsonNode.Parse(await client.GetStringAsync("/api/client/settings"))!.AsObject();

        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("en", root["language"]!["value"]!.GetValue<string>());
        Assert.Equal("hard", root["difficulty"]!["value"]!.GetValue<string>());
        Assert.True(root["showGmThoughts"]!.GetValue<bool>());
        Assert.False(root["audio"]!["musicEnabled"]!.GetValue<bool>());
        Assert.Equal(27, root["audio"]!["musicVolume"]!.GetValue<int>());
        Assert.Equal(115, root["accessibility"]!["fontScalePercent"]!.GetValue<int>());
        Assert.True(root["accessibility"]!["reducedMotion"]!.GetValue<bool>());
        Assert.True(root["accessibility"]!["contrastFriendly"]!.GetValue<bool>());
        Assert.True(root["locality"]!["localhostOnly"]!.GetValue<bool>());
        Assert.Contains("game_session", root["locality"]!["sessionLabel"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.False(root["locality"]!["gmBridgeEnabled"]!.GetValue<bool>());
        Assert.DoesNotContain(_rootPath, root.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-token-not-for-browser", root.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-shell-command", root.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClientSettingsEndpoint_UpdatesWhitelistedSettingsAndWritesGmProjection()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var response = await client.PostAsJsonAsync("/api/client/settings", new
        {
            language = "en",
            difficulty = "impossible",
            showGmThoughts = true,
            musicEnabled = false,
            musicVolume = 150,
            soundEnabled = false,
            soundVolume = -20,
            browserFontScalePercent = 175,
            browserReducedMotion = true,
            browserContrastFriendly = true
        });
        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var config = JsonNode.Parse(File.ReadAllText(Path.Combine(_rootPath, "game_session", "config.json")))!.AsObject();
        var gmProjection = JsonNode.Parse(File.ReadAllText(Path.Combine(_rootPath, "game_session", "game_state", "core", "game_settings.json")))!.AsObject();

        response.EnsureSuccessStatusCode();
        Assert.Equal("en", root["language"]!["value"]!.GetValue<string>());
        Assert.Equal("impossible", root["difficulty"]!["value"]!.GetValue<string>());
        Assert.True(root["showGmThoughts"]!.GetValue<bool>());
        Assert.Equal(100, root["audio"]!["musicVolume"]!.GetValue<int>());
        Assert.Equal(0, root["audio"]!["soundVolume"]!.GetValue<int>());
        Assert.Equal(140, root["accessibility"]!["fontScalePercent"]!.GetValue<int>());
        Assert.True(config["showGmThoughts"]!.GetValue<bool>());
        Assert.Equal("impossible", config["difficulty"]!.GetValue<string>());
        Assert.Equal(140, config["browserFontScalePercent"]!.GetValue<int>());
        Assert.True(gmProjection["impossibleMode"]!.GetValue<bool>());
        Assert.Equal("impossible", gmProjection["difficulty"]!.GetValue<string>());
    }

    [Fact]
    public async Task ClientSettingsEndpoint_BlocksGameplaySettingsWhenPendingTurnExists()
    {
        WriteSessionFile("config.json", """
        {
          "difficulty": "normal",
          "showGmThoughts": false
        }
        """);
        WriteSessionFile("input/turn_request.json", "{\"requestId\":\"pending-settings-guard\"}");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var response = await client.PostAsJsonAsync("/api/client/settings", new
        {
            difficulty = "hard",
            showGmThoughts = true
        });
        var body = await response.Content.ReadAsStringAsync();
        var config = JsonNode.Parse(File.ReadAllText(Path.Combine(_rootPath, "game_session", "config.json")))!.AsObject();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("заблокирован", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("normal", config["difficulty"]!.GetValue<string>());
        Assert.False(config["showGmThoughts"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ClientSettingsEndpoint_DoesNotReuseActiveBrowserWriteOwnerLock()
    {
        WriteSessionFile("config.json", """
        {
          "difficulty": "normal"
        }
        """);
        var activeOwnerId = $"browser:{Environment.MachineName}:{Environment.ProcessId}";
        var now = DateTime.UtcNow;
        WriteSessionFile(LocalUiSessionLockService.LockPath, $$"""
        {
          "schemaVersion": 1,
          "ownerId": "{{activeOwnerId}}",
          "ownerKind": "browser",
          "ownerLabel": "Active browser prompt form",
          "acquiredAtUtc": "{{now:O}}",
          "heartbeatAtUtc": "{{now:O}}",
          "leaseSeconds": 120,
          "lastOperation": "Browser prompt session"
        }
        """);
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var response = await client.PostAsJsonAsync("/api/client/settings", new
        {
            difficulty = "hard"
        });
        var body = await response.Content.ReadAsStringAsync();
        var lockJson = File.ReadAllText(Path.Combine(_rootPath, "game_session", LocalUiSessionLockService.LockPath));
        var config = JsonNode.Parse(File.ReadAllText(Path.Combine(_rootPath, "game_session", "config.json")))!.AsObject();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Active browser prompt form", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(activeOwnerId, lockJson, StringComparison.Ordinal);
        Assert.Equal("normal", config["difficulty"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task AudioSettingsEndpoint_LoadsSharedSettingsAndReturnsSafeCatalog()
    {
        WriteSessionFile("config.json", """
        {
          "musicEnabled": false,
          "musicVolume": 32,
          "soundEnabled": true,
          "soundVolume": 54
        }
        """);
        WriteRootFile("Music/Main Theme.mp3", "fake-mp3");
        WriteRootFile("Sounds/sound-notification.wav", "fake-wav");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var root = JsonNode.Parse(await client.GetStringAsync("/api/audio/settings"))!.AsObject();

        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.False(root["musicEnabled"]!.GetValue<bool>());
        Assert.Equal(32, root["musicVolume"]!.GetValue<int>());
        Assert.True(root["soundEnabled"]!.GetValue<bool>());
        Assert.Equal(54, root["soundVolume"]!.GetValue<int>());
        Assert.Contains("браузер", root["autoplayGuidance"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_rootPath, root.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        var mainMenu = root["playlists"]!.AsArray().Single(node => node!["id"]!.GetValue<string>() == "main-menu")!.AsObject();
        Assert.True(mainMenu["available"]!.GetValue<bool>());
        Assert.StartsWith("/api/audio/assets/", mainMenu["tracks"]!.AsArray()[0]!["url"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains(root["cues"]!.AsArray(), cue => cue?["id"]?.GetValue<string>() == "turn-ready" && cue["available"]!.GetValue<bool>());
    }

    [Fact]
    public async Task AudioSettingsEndpoint_UpdatesAndPersistsSharedSettingsWithClampedVolumes()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var response = await client.PostAsJsonAsync("/api/audio/settings", new
        {
            musicEnabled = false,
            musicVolume = 125,
            soundEnabled = false,
            soundVolume = -10
        });
        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var config = JsonNode.Parse(File.ReadAllText(Path.Combine(_rootPath, "game_session", "config.json")))!.AsObject();

        response.EnsureSuccessStatusCode();
        Assert.False(root["musicEnabled"]!.GetValue<bool>());
        Assert.Equal(100, root["musicVolume"]!.GetValue<int>());
        Assert.False(root["soundEnabled"]!.GetValue<bool>());
        Assert.Equal(0, root["soundVolume"]!.GetValue<int>());
        Assert.False(config["musicEnabled"]!.GetValue<bool>());
        Assert.Equal(100, config["musicVolume"]!.GetValue<int>());
        Assert.False(config["soundEnabled"]!.GetValue<bool>());
        Assert.Equal(0, config["soundVolume"]!.GetValue<int>());
    }

    [Fact]
    public void BrowserAudioService_SerializesSharedSettingsUpdates()
    {
        var source = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "WebUi", "BrowserAudioService.cs"));
        var normalizedSource = source.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("new SemaphoreSlim(1, 1)", normalizedSource, StringComparison.Ordinal);
        Assert.Contains("public async Task<BrowserAudioSettingsDto> BuildSettingsAsync()\n    {\n        await SettingsWriteGate.WaitAsync()", normalizedSource, StringComparison.Ordinal);
        Assert.Contains("await SettingsWriteGate.WaitAsync()", normalizedSource, StringComparison.Ordinal);
        Assert.Contains("SettingsWriteGate.Release()", normalizedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AudioAssetEndpoint_ServesOnlyCataloguedAssetsWithoutPathTraversal()
    {
        WriteRootFile("Music/Main Theme.mp3", "fake-mp3");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var settings = JsonNode.Parse(await client.GetStringAsync("/api/audio/settings"))!.AsObject();
        var assetId = settings["playlists"]!.AsArray()
            .Single(node => node!["id"]!.GetValue<string>() == "main-menu")!["tracks"]!.AsArray()[0]!["id"]!.GetValue<string>();
        using var ok = await client.GetAsync($"/api/audio/assets/{Uri.EscapeDataString(assetId)}");
        using var traversal = await client.GetAsync("/api/audio/assets/..%2Fconfig.json");

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("audio/mpeg", ok.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, traversal.StatusCode);
    }

    [Fact]
    public async Task SaveLoadEndpoint_LoadsOnlyMenuIssuedSaveIds()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "Сохранённая душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await CreateManualSaveAsync("browser-menu-save");
        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "Изменённая душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 9
        }
        """);
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var menu = JsonNode.Parse(await client.GetStringAsync("/api/main-menu"))!.AsObject();
        var saveId = menu["saves"]!.AsArray()[0]!["saveId"]!.GetValue<string>();
        using var loadResponse = await client.PostAsJsonAsync("/api/saves/load", new { saveId });
        var loaded = JsonNode.Parse((await loadResponse.Content.ReadAsStringAsync())!)!.AsObject();

        loadResponse.EnsureSuccessStatusCode();
        Assert.True(loaded["success"]!.GetValue<bool>());
        Assert.Equal("Сохранённая душа", loaded["menu"]!["session"]!["soulName"]!.GetValue<string>());
        Assert.Contains("Сохранённая душа", File.ReadAllText(Path.Combine(_rootPath, "game_session", "game_state", "meta", "soul_state.json")), StringComparison.Ordinal);

        using var invalidResponse = await client.PostAsJsonAsync("/api/saves/load", new { saveId = "manual:../../outside.zip" });
        var invalidBody = await invalidResponse.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(invalidBody), $"Expected a structured error body for invalid save IDs, got {(int)invalidResponse.StatusCode} {invalidResponse.StatusCode} with an empty body.");
        var invalid = JsonNode.Parse(invalidBody)!.AsObject();

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Contains("не найден", invalid["error"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveLoadEndpoint_BlocksLoadWhenBrowserWriteIsBlocked()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "Сохранённая душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await CreateManualSaveAsync("browser-menu-save");
        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "Изменённая душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 9
        }
        """);
        WriteSessionFile("input/turn_request.json", "{}");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var menu = JsonNode.Parse(await client.GetStringAsync("/api/main-menu"))!.AsObject();
        var loadAction = menu["actions"]!.AsArray().First(action =>
            string.Equals(action?["id"]?.GetValue<string>(), "load", StringComparison.Ordinal));
        var saveId = menu["saves"]!.AsArray()[0]!["saveId"]!.GetValue<string>();
        using var loadResponse = await client.PostAsJsonAsync("/api/saves/load", new { saveId });
        var loadBody = JsonNode.Parse(await loadResponse.Content.ReadAsStringAsync())!.AsObject();

        Assert.False(loadAction!["enabled"]!.GetValue<bool>());
        Assert.Contains("заблок", loadAction["disabledReason"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, loadResponse.StatusCode);
        Assert.Contains("заблок", loadBody["error"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Изменённая душа", File.ReadAllText(Path.Combine(_rootPath, "game_session", "game_state", "meta", "soul_state.json")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MainMenuEndpoint_UsesRussianReasonForMalformedSoulState()
    {
        WriteSessionFile("game_state/meta/soul_state.json", "{ not-json");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var menu = JsonNode.Parse(await client.GetStringAsync("/api/main-menu"))!.AsObject();
        var reason = menu["session"]!["continueReason"]!.GetValue<string>();

        Assert.False(menu["session"]!["canContinue"]!.GetValue<bool>());
        Assert.Contains("поврежд", reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("malformed", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RootEndpoint_ReturnsPlayerFacingBrowserMainMenu()
    {
        var fallbackFrontendRoot = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient.WebFrontend",
            "public");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(
            Array.Empty<string>(),
            new LocalWebUiHostOptions(_rootPath, url, fallbackFrontendRoot));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");

        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The Book of Eternity", html, StringComparison.Ordinal);
        Assert.Contains("id=\"main-menu\"", html, StringComparison.Ordinal);
        Assert.Contains("data-menu-action=\"continue\"", html, StringComparison.Ordinal);
        Assert.Contains("data-menu-action=\"new-game\"", html, StringComparison.Ordinal);
        Assert.Contains("data-menu-action=\"load\"", html, StringComparison.Ordinal);
        Assert.Contains("data-menu-action=\"options\"", html, StringComparison.Ordinal);
        Assert.Contains("data-menu-action=\"about\"", html, StringComparison.Ordinal);
        Assert.Contains("data-menu-action=\"exit\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"advanced-shell-toggle\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Local Web UI", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RootEndpoint_ServesConfiguredFrontendIndexAndStaticAssets()
    {
        var frontendRoot = Path.Combine(_rootPath, "frontend-dist");
        Directory.CreateDirectory(Path.Combine(frontendRoot, "assets"));
        await File.WriteAllTextAsync(Path.Combine(frontendRoot, "index.html"), """
        <!doctype html>
        <html lang="ru"><head><title>External Browser Shell</title></head><body><div id="root"></div><script type="module" src="/assets/app.js"></script></body></html>
        """);
        await File.WriteAllTextAsync(Path.Combine(frontendRoot, "assets", "app.js"), "console.log('frontend asset');");

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(
            Array.Empty<string>(),
            new LocalWebUiHostOptions(_rootPath, url, frontendRoot));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");
        var js = await client.GetStringAsync("/assets/app.js");
        var health = JsonNode.Parse(await client.GetStringAsync("/api/health"))!.AsObject();

        Assert.Contains("External Browser Shell", html, StringComparison.Ordinal);
        Assert.Contains("/assets/app.js", html, StringComparison.Ordinal);
        Assert.Contains("frontend asset", js, StringComparison.Ordinal);
        Assert.Equal("ok", health["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task FallbackEndpoint_ReturnsIndexForClientRoutesButNotApiOrAssetMisses()
    {
        var frontendRoot = Path.Combine(_rootPath, "frontend-dist");
        Directory.CreateDirectory(frontendRoot);
        await File.WriteAllTextAsync(Path.Combine(frontendRoot, "index.html"), "<!doctype html><title>SPA Shell</title>");

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(
            Array.Empty<string>(),
            new LocalWebUiHostOptions(_rootPath, url, frontendRoot));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var clientRoute = await client.GetStringAsync("/game/screen");
        using var missingApi = await client.GetAsync("/api/not-real");
        using var missingAsset = await client.GetAsync("/assets/not-real.js");

        Assert.Contains("SPA Shell", clientRoute, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, missingApi.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingAsset.StatusCode);
    }

    [Fact]
    public void FrontendAssets_PrefersReactIndexOverCopiedFallbackShellInBuildRoot()
    {
        var fakeRepo = Path.Combine(_rootPath, "fake-repo");
        var distRoot = Path.Combine(fakeRepo, "BookOfEternityClient.WebFrontend", "dist");
        Directory.CreateDirectory(distRoot);
        File.WriteAllText(Path.Combine(distRoot, "index.html"), "<!doctype html><title>React Shell</title>");
        File.WriteAllText(Path.Combine(distRoot, "local-web-ui-shell.html"), "<!doctype html><title>Fallback Shell</title>");

        var assets = LocalWebUiFrontendAssets.TryResolveBuildRoot(distRoot);

        Assert.NotNull(assets);
        Assert.False(assets.IsFallbackShell);
        Assert.Equal(Path.Combine(distRoot, "index.html"), assets.IndexPath);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task GameScreenEndpoint_ReturnsNarrativeChoicesLifecycleQteAndActionComposer()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "Экранная душа",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 3,
          "inkFeathers": { "current": 9 },
          "enlightenment": { "currentTier": "Сияющий знак" }
        }
        """);
        WriteSessionFile("game_state/meta/shining_abode_state.json", """
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 2 },
          "lightSparks": 4,
          "halls": [{ "hallId": "hall_dawn" }],
          "factions": [{ "factionId": "faction_scribes" }]
        }
        """);
        WriteSessionFile("game_state/world/current_location.json", """
        { "name": "Зал рассветных чернил" }
        """);
        WriteSessionFile("output/narrative_response.json", """
        { "response": "Сияние ложится на страницы." }
        """);
        WriteSessionFile("output/interface_updates.json", """
        {
          "dialogueOptions": [
            { "text": "Спросить хранителя о Вратах", "category": "диалог" },
            { "text": "Осмотреть зал", "category": "исследование" }
          ]
        }
        """);
        WriteSessionFile("output/debug_logs.json", """
        { "gm_thoughts_markdown": "GM видит скрытый конфликт фракций." }
        """);
        WriteSessionFile("game_state/combat/combat_log.json", """
        { "combat_log_markdown": "Последний духовный обмен завершён." }
        """);
        WriteSessionFile("input/turn_request.json", "{}");

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var root = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();

        Assert.Equal(2, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("shining-abode", root["theme"]!["key"]!.GetValue<string>());
        Assert.Equal("✨", root["theme"]!["icon"]!.GetValue<string>());
        Assert.Equal("Экранная душа", root["soul"]!["name"]!.GetValue<string>());
        Assert.Equal("Зал рассветных чернил", root["world"]!["location"]!.GetValue<string>());
        Assert.Contains("Сияние", root["narrative"]!["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("духовный", root["narrative"]!["combatLog"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, root["narrative"]!["dialogueOptions"]!.AsArray().Count);
        Assert.False(root["narrative"]!.AsObject().ContainsKey("gmThoughts"));
        Assert.False(root["actionComposer"]!["canSubmit"]!.GetValue<bool>());
        Assert.Contains("Ожидает", root["turnState"]!["title"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("NoScene", root["qte"]!["state"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task GameScreenEndpoint_DoesNotMutateQteRuntimeWhenRendering()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        { "soulName": "Read Only", "currentRealm": "Mortal World" }
        """);
        WriteSessionFile("game_state/control/qte_runtime.json", "{ malformed qte runtime");
        var runtimePath = Path.Combine(_rootPath, "game_session", "game_state", "control", "qte_runtime.json");
        var before = File.ReadAllText(runtimePath);
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var root = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();

        Assert.Equal("NoScene", root["qte"]!["state"]!.GetValue<string>());
        Assert.True(File.Exists(runtimePath));
        Assert.Equal(before, File.ReadAllText(runtimePath));
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task GameScreenEndpoint_DoesNotExposeDebugGmThoughtsInDefaultDto()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        { "soulName": "Hidden GM", "currentRealm": "Mortal World" }
        """);
        WriteSessionFile("output/debug_logs.json", """
        { "gm_thoughts_markdown": "GM secret should remain advanced-only." }
        """);

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var root = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();

        Assert.False(root["narrative"]!.AsObject().ContainsKey("gmThoughts"));
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task GameScreenEndpoint_ReportsReadyAndErrorTurnStatesDistinctly()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        { "soulName": "Turn Soul", "currentRealm": "Chaos Sea" }
        """);
        WriteSessionFile("ready/turn_complete.json", "{} ");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var readyRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("ready-gm-response", readyRoot["turnState"]!["state"]!.GetValue<string>());
        Assert.Contains("готов", readyRoot["turnState"]!["title"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.False(readyRoot["actionComposer"]!["canSubmit"]!.GetValue<bool>());

        File.Delete(Path.Combine(_rootPath, "game_session", "ready", "turn_complete.json"));
        WriteSessionFile("ready/turn_error.json", """
        { "error": "GM timeout" }
        """);
        var errorRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("gm-turn-error", errorRoot["turnState"]!["state"]!.GetValue<string>());
        Assert.Contains("ошиб", errorRoot["turnState"]!["title"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.False(errorRoot["actionComposer"]!["canSubmit"]!.GetValue<bool>());
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task GameScreenEndpoint_DisablesComposerWhenValidationRequiresRepair()
    {
        WriteSessionFile("game_state/meta/soul_state.json", "not json");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var root = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();

        Assert.Equal("validation-errors", root["turnState"]!["state"]!.GetValue<string>());
        Assert.True(root["turnState"]!["validationLabel"]!.GetValue<string>().Contains("ошиб", StringComparison.OrdinalIgnoreCase));
        Assert.False(root["actionComposer"]!["canSubmit"]!.GetValue<bool>());
        Assert.Equal("repair-required", root["actionComposer"]!["mode"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task GameScreenEndpoint_MapsPendingArtifactsToPlayerFacingLifecyclePhases()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        { "soulName": "Lifecycle Soul", "currentRealm": "Mortal World" }
        """);
        WriteSessionFile("input/turn_request.json", "{}");

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var waitingRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("waiting-gm", waitingRoot["turnState"]!["phase"]!.GetValue<string>());
        Assert.Equal("Ожидаем ответ ГМа", waitingRoot["turnState"]!["phaseLabel"]!.GetValue<string>());
        Assert.Equal("warning", waitingRoot["turnState"]!["severity"]!.GetValue<string>());
        Assert.Contains("ГМ", waitingRoot["turnState"]!["playerGuidance"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            waitingRoot["turnState"]!["recommendedActions"]!.AsArray(),
            action => action!["id"]!.GetValue<string>() == "wait-for-gm" && action!["surface"]!.GetValue<string>() == "player-default");
        Assert.Contains(
            waitingRoot["turnState"]!["knownPhases"]!.AsArray(),
            phase => phase!["id"]!.GetValue<string>() == "cancelled");

        File.Delete(Path.Combine(_rootPath, "game_session", "input", "turn_request.json"));
        WriteSessionFile("ready/turn_complete.json", "{}");
        var readyRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("ready", readyRoot["turnState"]!["phase"]!.GetValue<string>());
        Assert.Equal("success", readyRoot["turnState"]!["severity"]!.GetValue<string>());
        Assert.Contains("примите", readyRoot["turnState"]!["playerGuidance"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        File.Delete(Path.Combine(_rootPath, "game_session", "ready", "turn_complete.json"));
        WriteSessionFile("ready/turn_error.json", "{ \"error\": \"GM timeout\" }");
        var errorRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("error-restored", errorRoot["turnState"]!["phase"]!.GetValue<string>());
        Assert.Equal("error", errorRoot["turnState"]!["severity"]!.GetValue<string>());
        Assert.Contains("починку", errorRoot["turnState"]!["playerGuidance"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        WriteSessionFile("game_state/control/pending_turn_snapshot.json", "{}");
        var repairRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("repair-required", repairRoot["turnState"]!["phase"]!.GetValue<string>());
        Assert.Equal("pending-turn-repair", repairRoot["turnState"]!["state"]!.GetValue<string>());
        Assert.Contains("починку", repairRoot["turnState"]!["playerGuidance"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task GameScreenEndpoint_ExplainsValidationAndLocalLockLifecyclePhases()
    {
        WriteSessionFile("game_state/meta/soul_state.json", "not json");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var validationRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("validation-failed", validationRoot["turnState"]!["phase"]!.GetValue<string>());
        Assert.Equal("error", validationRoot["turnState"]!["severity"]!.GetValue<string>());
        Assert.DoesNotContain("game_state/meta/soul_state.json", validationRoot["turnState"]!["playerGuidance"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        Directory.CreateDirectory(Path.Combine(_rootPath, "game_session", "game_state", "meta"));
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "game_session", "game_state", "meta", "soul_state.json"), """
        { "soulName": "Locked Soul", "currentRealm": "Mortal World" }
        """);
        Directory.CreateDirectory(Path.Combine(_rootPath, "game_session", "game_state", "control"));
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "game_session", LocalUiSessionLockService.LockPath), """
        {
          "schemaVersion": 1,
          "ownerId": "browser:test",
          "ownerKind": "browser",
          "ownerLabel": "Browser test",
          "acquiredAtUtc": "2099-01-01T00:00:00Z",
          "heartbeatAtUtc": "2099-01-01T00:00:00Z",
          "leaseSeconds": 120,
          "lastOperation": "submitting turn"
        }
        """);

        var lockedRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("turn-submitted", lockedRoot["turnState"]!["phase"]!.GetValue<string>());
        Assert.Equal("warning", lockedRoot["turnState"]!["severity"]!.GetValue<string>());
        Assert.False(lockedRoot["actionComposer"]!["canSubmit"]!.GetValue<bool>());
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task GameScreenEndpoint_ReadsTurnNumberFromStoryHistory()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        { "soulName": "Story Soul", "currentRealm": "Mortal World" }
        """);
        WriteSessionFile("stories/chaos_sea.jsonl", """
        {"turn":5,"realm":"Chaos Sea","player":"Первый ход","narrative":"Начало"}
        {"turn":17,"realm":"Chaos Sea","player":"Семнадцатый ход","narrative":"Продолжение"}
        """);
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var root = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();

        Assert.Equal(17, root["world"]!["turnNumber"]!.GetValue<int>());
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task RootEndpoint_DefaultPlayerAreaContainsGameScreenAndPrimaryActionComposer()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");
        var advancedIndex = html.IndexOf("<section id=\"advanced-shell\"", StringComparison.Ordinal);
        Assert.True(advancedIndex > 0, "Advanced shell must follow default player game content.");
        var playerDefault = html[..advancedIndex];

        Assert.Contains("id=\"game-screen\"", playerDefault, StringComparison.Ordinal);
        Assert.Contains("id=\"player-action-composer\"", playerDefault, StringComparison.Ordinal);
        Assert.Contains("name=\"player-action\"", playerDefault, StringComparison.Ordinal);
        Assert.Contains("renderGameScreen", html, StringComparison.Ordinal);
        Assert.Contains("loadGameScreen", html, StringComparison.Ordinal);
        Assert.Contains("submitPlayerAction", html, StringComparison.Ordinal);
        Assert.Contains("/api/game-screen", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Командная палитра", playerDefault, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"command-form\"", playerDefault, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task RootEndpoint_DefaultComposerDoesNotAutoExecuteSlashCommands()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");

        Assert.Contains("actionText.startsWith('/')", html, StringComparison.Ordinal);
        Assert.DoesNotContain("executeCommand(actionText)", html, StringComparison.Ordinal);
        Assert.Contains("prefillAdvancedCommand", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RootEndpoint_KeepsDebugToolsInsideExplicitAdvancedPanel()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");
        var advancedIndex = html.IndexOf("<section id=\"advanced-shell\"", StringComparison.Ordinal);

        Assert.True(advancedIndex > 0, "The advanced panel must be a separate section after the default player menu.");
        var playerDefault = html[..advancedIndex];
        var advancedPanel = html[advancedIndex..];

        Assert.Contains("id=\"advanced-shell-toggle\"", playerDefault, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"advanced-shell\"", playerDefault, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"false\"", playerDefault, StringComparison.Ordinal);
        Assert.DoesNotContain("Командная палитра", playerDefault, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Диагностика", playerDefault, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/lifecycle", playerDefault, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/explorer", playerDefault, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-command=\"/debug\"", playerDefault, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("id=\"advanced-shell\" class=\"advanced-shell\" hidden", advancedPanel, StringComparison.Ordinal);
        Assert.Contains("Технический режим", advancedPanel, StringComparison.Ordinal);
        Assert.Contains("id=\"command-form\"", advancedPanel, StringComparison.Ordinal);
        Assert.Contains("id=\"lifecycle-panel\"", advancedPanel, StringComparison.Ordinal);
        Assert.Contains("data-command=\"/validate\"", advancedPanel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RootEndpoint_PlayerMenuActionsDoNotAutomaticallyOpenAdvancedDiagnostics()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");
        var continueCase = ExtractSwitchCase(html, "continue", "new-game");
        var newGameCase = ExtractSwitchCase(html, "new-game", "load");

        Assert.DoesNotContain("showAdvancedShell", continueCase, StringComparison.Ordinal);
        Assert.DoesNotContain("showAdvancedShell", newGameCase, StringComparison.Ordinal);
        Assert.Contains("showPlayerAction", continueCase, StringComparison.Ordinal);
        Assert.Contains("showPlayerAction", newGameCase, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RootEndpoint_IncludesConcisePlayerErrorRendererWithExpandableDetails()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");

        Assert.Contains("function renderPlayerError", html, StringComparison.Ordinal);
        Assert.Contains("document.createElement('details')", html, StringComparison.Ordinal);
        Assert.Contains("Подробности", html, StringComparison.Ordinal);
        Assert.Contains("renderPlayerError('Главное меню недоступно'", html, StringComparison.Ordinal);
        Assert.Contains("renderPlayerError('Сохранение не загружено'", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RootEndpoint_IncludesCommandRendererAssets()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var response = await client.GetAsync("/api/media/" + Uri.EscapeDataString(mediaId));
        var json = JsonNode.Parse((await response.Content.ReadAsStringAsync())!)!.AsObject();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("разреш", json["error"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MediaGenerateEndpoint_ReturnsDisabledMessageWhenImageProviderOff()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var response = await client.PostAsJsonAsync("/api/media/generate", new
        {
            prompt = "Портрет героя в сиянии свечей",
            entityType = "npc",
            entityKey = "hero"
        });
        var root = JsonNode.Parse((await response.Content.ReadAsStringAsync())!)!.AsObject();

        response.EnsureSuccessStatusCode();
        Assert.False(root["success"]!.GetValue<bool>());
        Assert.Contains("отключена", root["errorMessage"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MediaGenerateEndpoint_ReturnsExistingMediaReferenceWhenImageAlreadyExists()
    {
        WriteSessionImage("images/npcs/hero.png");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        app.Services.GetRequiredService<GameSettings>().ImageProvider = "pollinations";
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var response = await client.PostAsJsonAsync("/api/media/generate", new
        {
            prompt = "Портрет героя в сиянии свечей",
            entityType = "npc",
            entityKey = "hero"
        });
        var root = JsonNode.Parse((await response.Content.ReadAsStringAsync())!)!.AsObject();

        response.EnsureSuccessStatusCode();
        Assert.True(root["success"]!.GetValue<bool>());
        Assert.Equal(LocalMediaService.CreateMediaIdForRelativePath("images/npcs/hero.png"), root["mediaId"]!.GetValue<string>());
        Assert.Equal("/api/media/" + Uri.EscapeDataString(LocalMediaService.CreateMediaIdForRelativePath("images/npcs/hero.png")), root["url"]!.GetValue<string>());
        Assert.Null(root["errorMessage"]);
    }

    [Fact]
    public async Task RootEndpoint_IncludesLifecycleDashboardAssets()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
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

    private async Task CreateManualSaveAsync(string saveName)
    {
        var fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        var stateManager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);
        await stateManager.RefreshGameStateAsync();
        var saveLoad = new SaveLoadService(fs, stateManager, NullLogger<SaveLoadService>.Instance);
        var saved = await saveLoad.SaveGameAsync(saveName, "Browser main menu save/load test");
        Assert.True(saved, "The test fixture must be able to create a manual save before exercising the browser load endpoint.");
    }

    private static string ExtractSwitchCase(string html, string caseName, string nextCaseName)
    {
        var start = html.IndexOf($"case '{caseName}':", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected switch case for {caseName}.");
        var end = html.IndexOf($"case '{nextCaseName}':", start + 1, StringComparison.Ordinal);
        Assert.True(end > start, $"Expected switch case for {nextCaseName} after {caseName}.");
        return html[start..end];
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

    private void WriteRootFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
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
