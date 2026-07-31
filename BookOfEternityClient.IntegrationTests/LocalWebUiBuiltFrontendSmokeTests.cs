using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LocalWebUiBuiltFrontendSmokeTests : IDisposable
{
    private readonly string _rootPath;

    public LocalWebUiBuiltFrontendSmokeTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-built-web-ui-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiBuiltFrontend")]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics()
    {
        var frontendDist = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "dist");
        var indexPath = Path.Combine(frontendDist, "index.html");
        Assert.True(
            File.Exists(indexPath),
            $"Missing built browser frontend at {indexPath}. Run `npm run verify --prefix BookOfEternityClient.WebFrontend` before the built-frontend smoke test.");

        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "CI-душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 5,
          "inkFeathers": { "current": 3 }
        }
        """);
        WriteSessionFile("game_state/world/current_location.json", """
        {
          "name": "Проверочный тракт"
        }
        """);
        WriteSessionFile("output/narrative_response.json", """
        {
          "response": "Сборка браузера открывает локальную книгу без сети."
        }
        """);

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(
            Array.Empty<string>(),
            new LocalWebUiHostOptions(_rootPath, url, frontendDist));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var root = await CaptureAsync(client, "/");
        var gameRoute = await CaptureAsync(client, "/game");
        var menuResponse = await CaptureAsync(client, "/api/main-menu");
        var sessionResponse = await CaptureAsync(client, "/api/session");
        var screenResponse = await CaptureAsync(client, "/api/game-screen");
        var missingApi = await CaptureAsync(client, "/api/not-real");
        var missingAsset = await CaptureAsync(client, "/assets/not-real.js");
        var assetPaths = ExtractAssetPaths(root.Body);
        var assetResponses = new List<SmokeResponse>();
        foreach (var assetPath in assetPaths)
            assetResponses.Add(await CaptureAsync(client, assetPath));
        var browserUiAssetPaths = new[]
        {
            "/browser-ui-assets/scene-hero-fallback.png",
            "/browser-ui-assets/gallery-empty-archive.png",
            "/browser-ui-assets/status-soul-vignette.png"
        };
        var browserUiAssetResponses = new List<SmokeResponse>();
        foreach (var browserUiAssetPath in browserUiAssetPaths)
            browserUiAssetResponses.Add(await CaptureAsync(client, browserUiAssetPath));

        var artifactRoot = PrepareArtifactDirectory();
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "root.html"), root.Body);
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "game-route.html"), gameRoute.Body);
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "main-menu.json"), menuResponse.Body);
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "session.json"), sessionResponse.Body);
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "game-screen.json"), screenResponse.Body);
        await File.WriteAllTextAsync(
            Path.Combine(artifactRoot, "network.json"),
            JsonSerializer.Serialize(
                new
                {
                    baseUrl = url,
                    frontendDist,
                    requests = new[]
                    {
                        root.ToArtifact(),
                        gameRoute.ToArtifact(),
                        menuResponse.ToArtifact(),
                        sessionResponse.ToArtifact(),
                        screenResponse.ToArtifact(),
                        missingApi.ToArtifact(),
                        missingAsset.ToArtifact()
                    }.Concat(assetResponses.Select(response => response.ToArtifact()))
                    .Concat(browserUiAssetResponses.Select(response => response.ToArtifact()))
                },
                new JsonSerializerOptions { WriteIndented = true }));
        var navigationArtifactPath = Path.Combine(artifactRoot, "navigation-ia.html");
        var detailSurfaceArtifactPath = Path.Combine(artifactRoot, "detail-surfaces.html");
        var rebornPanelsArtifactPath = Path.Combine(artifactRoot, "reborn-panels.html");
        var firstScreenVisualQaArtifactPath = Path.Combine(artifactRoot, "first-screen-visual-qa.html");
        var startNewChapterArtifactPath = Path.Combine(artifactRoot, "start-new-chapter-flow.html");
        var browserImagegenAssetsArtifactPath = Path.Combine(artifactRoot, "browser-imagegen-assets.html");

        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Equal(HttpStatusCode.OK, gameRoute.StatusCode);
        Assert.Equal(HttpStatusCode.OK, menuResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, screenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingApi.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingAsset.StatusCode);

        Assert.Contains("<div id=\"root\"></div>", root.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/assets/", root.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Книга Вечности: Перерождение", root.Body, StringComparison.Ordinal);
        Assert.Equal(root.Body, gameRoute.Body);
        Assert.NotEmpty(assetPaths);
        Assert.All(assetResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Contains(assetResponses, response => response.Path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) &&
            response.ContentType?.Contains("javascript", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(assetResponses, response => response.Path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) &&
            response.ContentType?.Contains("text/css", StringComparison.OrdinalIgnoreCase) == true);
        Assert.All(browserUiAssetResponses, response =>
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("image/png", response.ContentType, StringComparison.OrdinalIgnoreCase);
            Assert.True(response.Body.Length > 16 * 1024, $"{response.Path} should serve a real local visual asset.");
        });

        var menu = JsonNode.Parse(menuResponse.Body)!.AsObject();
        var session = JsonNode.Parse(sessionResponse.Body)!.AsObject();
        var screen = JsonNode.Parse(screenResponse.Body)!.AsObject();
        var frontendSourceRoot = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "src");
        var appSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "App.tsx"));
        var assetModuleSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "browserUiAssets.ts"));
        var tabBarConfigSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "components", "tabBarConfig.ts"));
        var tabBarSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "components", "TabBar.tsx"));
        var sceneHeroSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "components", "SceneHero.tsx"));
        var cinematicSceneHeroSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "components", "decorative", "CinematicSceneHero.tsx"));
        var launcherSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "components", "GameLauncher.tsx"));
        var blockRendererSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "components", "BlockRenderer.tsx"));
        var sceneViewSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "components", "SceneView.tsx"));
        var statusViewSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "components", "StatusView.tsx"));
        var helpViewSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "components", "HelpView.tsx"));
        var settingsViewSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "components", "SettingsView.tsx"));
        var unifiedInputSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "components", "UnifiedInput.tsx"));
        var promptFormSource = File.ReadAllText(Path.Combine(frontendSourceRoot, "components", "PromptForm.tsx"));
        var currentShellSources = string.Join(
            '\n',
            appSource,
            tabBarConfigSource,
            tabBarSource,
            launcherSource,
            sceneViewSource,
            statusViewSource,
            helpViewSource,
            settingsViewSource,
            unifiedInputSource,
            promptFormSource);

        await File.WriteAllTextAsync(firstScreenVisualQaArtifactPath, BuildFirstScreenVisualQaArtifact(appSource, tabBarConfigSource, launcherSource));
        await File.WriteAllTextAsync(navigationArtifactPath, BuildNavigationIaArtifact(tabBarConfigSource));
        await File.WriteAllTextAsync(detailSurfaceArtifactPath, BuildDetailSurfaceArtifact(statusViewSource));
        await File.WriteAllTextAsync(rebornPanelsArtifactPath, BuildRebornPanelsArtifact(statusViewSource));
        await File.WriteAllTextAsync(startNewChapterArtifactPath, BuildStartNewChapterFlowArtifact(launcherSource, promptFormSource));
        await File.WriteAllTextAsync(browserImagegenAssetsArtifactPath, BuildBrowserImagegenAssetsArtifact(assetModuleSource, sceneHeroSource, cinematicSceneHeroSource, sceneViewSource, blockRendererSource, statusViewSource));

        Assert.True(session["localOnly"]!.GetValue<bool>());
        Assert.Equal("CI-душа", menu["session"]!["soulName"]!.GetValue<string>());
        Assert.Equal("CI-душа", screen["soul"]!["name"]!.GetValue<string>());
        Assert.Equal("Проверочный тракт", screen["world"]!["location"]!.GetValue<string>());
        Assert.Contains("локальную книгу", screen["narrative"]!["text"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("import { TabBar } from './components/TabBar';", appSource, StringComparison.Ordinal);
        Assert.Contains("<GameLauncher menu={menu} />", appSource, StringComparison.Ordinal);
        Assert.Contains("const isPracticeRoute = activeRoute === 'practice';", appSource, StringComparison.Ordinal);
        Assert.Contains("{!isLauncherRoute && !isPracticeRoute && !isDarenShowcaseRoute && <UnifiedInput />}", appSource, StringComparison.Ordinal);
        Assert.Contains("tabNav.map((tab)", tabBarSource, StringComparison.Ordinal);
        Assert.Contains("Открыть книгу", launcherSource, StringComparison.Ordinal);
        Assert.Contains("Продолжить главу", launcherSource, StringComparison.Ordinal);
        Assert.Contains("Загрузить сохранение", launcherSource, StringComparison.Ordinal);
        Assert.Contains("Начать новую главу", launcherSource, StringComparison.Ordinal);
        Assert.Contains("Настроить книгу", launcherSource, StringComparison.Ordinal);
        Assert.Contains("className=\"scene-narrative scene-post\"", sceneViewSource, StringComparison.Ordinal);
        Assert.Contains("className=\"scene-quick-actions\"", sceneViewSource, StringComparison.Ordinal);
        Assert.Contains("Опишите действие или введите /команду...", unifiedInputSource, StringComparison.Ordinal);
        Assert.Contains("Художественный пост", unifiedInputSource, StringComparison.Ordinal);
        Assert.Contains("GROUP_LABELS", helpViewSource, StringComparison.Ordinal);
        Assert.Contains("Расширенный режим", settingsViewSource, StringComparison.Ordinal);
        Assert.Contains("browserApi.executeExplorerCommand({ command: startCommand", launcherSource, StringComparison.Ordinal);
        Assert.Contains("browserApi.submitPromptSession", launcherSource, StringComparison.Ordinal);
        Assert.Contains("renderPromptControl", promptFormSource, StringComparison.Ordinal);
        Assert.DoesNotContain("setAdvancedEnabled(true)", currentShellSources, StringComparison.Ordinal);
        Assert.DoesNotContain("action.advancedCommand}", currentShellSources, StringComparison.Ordinal);
        Assert.DoesNotContain("C# каталога команд", currentShellSources, StringComparison.Ordinal);
        Assert.DoesNotContain("C# протоколом", currentShellSources, StringComparison.Ordinal);
        Assert.DoesNotContain("C# DTO", currentShellSources, StringComparison.Ordinal);

        Assert.True(File.Exists(navigationArtifactPath), $"Missing browser navigation visual smoke artifact at {navigationArtifactPath}");
        var navigationArtifact = await File.ReadAllTextAsync(navigationArtifactPath);
        Assert.Contains("data-artifact=\"browser-navigation-ia\"", navigationArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"desktop\"", navigationArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"mobile\"", navigationArtifact, StringComparison.Ordinal);
        Assert.Contains("Сцена → Статус → Помощь → Настройки", navigationArtifact, StringComparison.Ordinal);
        Assert.Contains("Текущий ход, повествование и быстрые действия.", navigationArtifact, StringComparison.Ordinal);
        Assert.Contains("Расширенный режим", navigationArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("Debug", navigationArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Network", navigationArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command coverage", navigationArtifact, StringComparison.OrdinalIgnoreCase);

        Assert.True(File.Exists(detailSurfaceArtifactPath), $"Missing browser detail-surface visual smoke artifact at {detailSurfaceArtifactPath}");
        var detailSurfaceArtifact = await File.ReadAllTextAsync(detailSurfaceArtifactPath);
        Assert.Contains("data-artifact=\"browser-detail-surfaces\"", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"desktop\"", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("data-state=\"status-overview\"", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("data-state=\"afterlife-available\"", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"mobile\"", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("Персонаж", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("Душа", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("Мир", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("Посмертие", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("Debug", detailSurfaceArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/", detailSurfaceArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw JSON", detailSurfaceArtifact, StringComparison.OrdinalIgnoreCase);

        Assert.True(File.Exists(rebornPanelsArtifactPath), $"Missing browser Reborn panels visual smoke artifact at {rebornPanelsArtifactPath}");
        var rebornPanelsArtifact = await File.ReadAllTextAsync(rebornPanelsArtifactPath);
        Assert.Contains("data-artifact=\"browser-reborn-panels\"", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"desktop\"", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-state=\"mortal-locked\"", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-state=\"afterlife-active\"", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"mobile\"", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("Посмертие Reborn", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("Сияющая Обитель", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("Море Хаоса", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("Посмертные панели откроются", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("pending_", rebornPanelsArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("control/", rebornPanelsArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/", rebornPanelsArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw JSON", rebornPanelsArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Debug", rebornPanelsArtifact, StringComparison.OrdinalIgnoreCase);

        Assert.True(File.Exists(firstScreenVisualQaArtifactPath), $"Missing browser first-screen visual QA artifact at {firstScreenVisualQaArtifactPath}");
        var firstScreenVisualQaArtifact = await File.ReadAllTextAsync(firstScreenVisualQaArtifactPath);
        Assert.Contains("data-artifact=\"browser-first-screen-visual-qa\"", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"desktop\"", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"mobile\"", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Книга Вечности: Перерождение", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Открыть книгу", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Продолжить главу", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Загрузить сохранение", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Настроить книгу", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Сцена → Статус → Помощь → Настройки", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("current minimal tab shell", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("advanced debug secondary", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-state=\"fresh-empty\"", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Активной главы пока нет", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("Ожидание ГМа", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("Локальный игровой клиент", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("источник истины", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Главное меню недоступно", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug dashboard", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug shell", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Network", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command coverage", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span>book</span>", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span>flame</span>", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        foreach (var emojiIcon in new[] { "✦", "📖", "⚡", "📊", "❓", "🕯️", "🗺️", "✍️", "🎒", "🎞️", "⚙️" })
        {
            Assert.DoesNotContain(emojiIcon, firstScreenVisualQaArtifact, StringComparison.Ordinal);
        }

        Assert.True(File.Exists(startNewChapterArtifactPath), $"Missing browser start-new-chapter visual smoke artifact at {startNewChapterArtifactPath}");
        var startNewChapterArtifact = await File.ReadAllTextAsync(startNewChapterArtifactPath);
        Assert.Contains("data-artifact=\"browser-start-new-chapter-flow\"", startNewChapterArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"desktop\"", startNewChapterArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"mobile\"", startNewChapterArtifact, StringComparison.Ordinal);
        Assert.Contains("Начать новую главу", startNewChapterArtifact, StringComparison.Ordinal);
        Assert.Contains("Форма новой главы", startNewChapterArtifact, StringComparison.Ordinal);
        Assert.Contains("Режим подготовки мира", startNewChapterArtifact, StringComparison.Ordinal);
        Assert.Contains("Название мира", startNewChapterArtifact, StringComparison.Ordinal);
        Assert.Contains("Директивы мира", startNewChapterArtifact, StringComparison.Ordinal);
        Assert.Contains("Отправить форму", startNewChapterArtifact, StringComparison.Ordinal);
        Assert.Contains("truthful unavailable state", startNewChapterArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/world_setup", startNewChapterArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/", startNewChapterArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw JSON", startNewChapterArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", startNewChapterArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("screenshot", startNewChapterArtifact, StringComparison.OrdinalIgnoreCase);

        Assert.True(File.Exists(browserImagegenAssetsArtifactPath), $"Missing browser imagegen asset visual smoke artifact at {browserImagegenAssetsArtifactPath}");
        var browserImagegenAssetsArtifact = await File.ReadAllTextAsync(browserImagegenAssetsArtifactPath);
        Assert.Contains("data-artifact=\"browser-imagegen-assets\"", browserImagegenAssetsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"desktop\"", browserImagegenAssetsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"mobile\"", browserImagegenAssetsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-state=\"scene-fallback\"", browserImagegenAssetsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-state=\"gallery-empty\"", browserImagegenAssetsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-state=\"status-ambient\"", browserImagegenAssetsArtifact, StringComparison.Ordinal);
        Assert.Contains("local visual-smoke artifact", browserImagegenAssetsArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not an automated screenshot", browserImagegenAssetsArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scene-hero-fallback.png", browserImagegenAssetsArtifact, StringComparison.Ordinal);
        Assert.Contains("gallery-empty-archive.png", browserImagegenAssetsArtifact, StringComparison.Ordinal);
        Assert.Contains("status-soul-vignette.png", browserImagegenAssetsArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/", browserImagegenAssetsArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", browserImagegenAssetsArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw JSON", browserImagegenAssetsArtifact, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiBuiltFrontend")]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task BuiltFrontendSmoke_GeneratesMainMenuBackgroundArtArtifact()
    {
        var frontendDist = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "dist");
        var indexPath = Path.Combine(frontendDist, "index.html");
        var publicRoot = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "public");
        var backgroundPath = Path.Combine(publicRoot, "main-menu-bg.webp");
        var sourceNotePath = Path.Combine(publicRoot, "main-menu-bg.source.md");
        var launcher = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "src", "components", "GameLauncher.tsx"));
        var styles = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "src", "styles", "components.css"));
        var artifactRoot = PrepareArtifactDirectory();
        var mainMenuBackgroundArtifactPath = Path.Combine(artifactRoot, "main-menu-background-art.html");
        var homeLauncherHierarchyArtifactPath = Path.Combine(artifactRoot, "home-launcher-hierarchy.html");

        Assert.True(
            File.Exists(indexPath),
            $"Missing built browser frontend at {indexPath}. Run `npm run verify --prefix BookOfEternityClient.WebFrontend` before the built-frontend smoke test.");
        var builtScriptBundle = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(frontendDist, "*.js", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        Assert.True(File.Exists(backgroundPath), $"Missing local launcher background art at {backgroundPath}");
        Assert.True(File.Exists(sourceNotePath), $"Missing launcher background source note at {sourceNotePath}");
        Assert.Contains("<div className=\"launcher-art-bg\" aria-hidden=\"true\">", launcher, StringComparison.Ordinal);
        Assert.Contains("src=\"/main-menu-bg.webp\"", launcher, StringComparison.Ordinal);
        Assert.Contains("alt=\"\"", launcher, StringComparison.Ordinal);
        Assert.Contains("onError={(event) => { event.currentTarget.hidden = true; }}", launcher, StringComparison.Ordinal);
        Assert.Contains("data-action-state={disabled ? 'disabled' : 'enabled'}", launcher, StringComparison.Ordinal);
        Assert.Contains("launcher-menu__item-affordance", launcher, StringComparison.Ordinal);
        Assert.Contains("launcher-session-warning", launcher, StringComparison.Ordinal);
        Assert.Contains(".launcher-art-bg img", styles, StringComparison.Ordinal);
        Assert.Contains(".launcher-art-bg::before", styles, StringComparison.Ordinal);
        Assert.Contains("object-fit: cover;", styles, StringComparison.Ordinal);
        Assert.Contains("object-position: center 30%;", styles, StringComparison.Ordinal);
        Assert.Contains("filter: saturate(0.7) brightness(0.5);", styles, StringComparison.Ordinal);
        Assert.Contains(".launcher-art-bg::after", styles, StringComparison.Ordinal);
        Assert.Contains("linear-gradient(to bottom", styles, StringComparison.Ordinal);
        Assert.Contains(".launcher-session-warning", styles, StringComparison.Ordinal);
        Assert.Contains("main-menu-bg.webp", builtScriptBundle, StringComparison.Ordinal);
        Assert.Contains("launcher-art-bg", builtScriptBundle, StringComparison.Ordinal);
        Assert.Contains("launcher-menu__item-affordance", builtScriptBundle, StringComparison.Ordinal);
        Assert.Contains("Открыть книгу", builtScriptBundle, StringComparison.Ordinal);
        Assert.Contains("Продолжить главу", builtScriptBundle, StringComparison.Ordinal);

        await File.WriteAllTextAsync(mainMenuBackgroundArtifactPath, BuildMainMenuBackgroundArtifact());
        await File.WriteAllTextAsync(homeLauncherHierarchyArtifactPath, BuildHomeLauncherHierarchyArtifact());

        Assert.True(File.Exists(mainMenuBackgroundArtifactPath), $"Missing main-menu background-art visual smoke artifact at {mainMenuBackgroundArtifactPath}");
        var mainMenuBackgroundArtifact = await File.ReadAllTextAsync(mainMenuBackgroundArtifactPath);
        Assert.Contains("data-artifact=\"main-menu-background-art\"", mainMenuBackgroundArtifact, StringComparison.Ordinal);
        Assert.Contains("data-background=\"enabled\"", mainMenuBackgroundArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"desktop\"", mainMenuBackgroundArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"narrow\"", mainMenuBackgroundArtifact, StringComparison.Ordinal);
        Assert.Contains("main-menu-bg.webp", mainMenuBackgroundArtifact, StringComparison.Ordinal);
        Assert.Contains("dependency-light local HTML visual smoke artifact", mainMenuBackgroundArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not an automated screenshot", mainMenuBackgroundArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BookOfEternityClient.WebFrontend/public/main-menu-bg.webp", mainMenuBackgroundArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("external runtime dependency", mainMenuBackgroundArtifact, StringComparison.OrdinalIgnoreCase);

        Assert.True(File.Exists(homeLauncherHierarchyArtifactPath), $"Missing home-launcher hierarchy visual smoke artifact at {homeLauncherHierarchyArtifactPath}");
        var homeLauncherHierarchyArtifact = await File.ReadAllTextAsync(homeLauncherHierarchyArtifactPath);
        Assert.Contains("data-artifact=\"home-launcher-hierarchy\"", homeLauncherHierarchyArtifact, StringComparison.Ordinal);
        Assert.Contains("data-action-state=\"enabled\"", homeLauncherHierarchyArtifact, StringComparison.Ordinal);
        Assert.Contains("data-action-state=\"disabled\"", homeLauncherHierarchyArtifact, StringComparison.Ordinal);
        Assert.Contains("data-validation=\"warning\"", homeLauncherHierarchyArtifact, StringComparison.Ordinal);
        Assert.Contains("dependency-light local HTML visual smoke artifact", homeLauncherHierarchyArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not an automated screenshot", homeLauncherHierarchyArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api", homeLauncherHierarchyArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", homeLauncherHierarchyArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw JSON", homeLauncherHierarchyArtifact, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<SmokeResponse> CaptureAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        return new SmokeResponse(path, response.StatusCode, response.Content.Headers.ContentType?.ToString(), body);
    }

    private static string[] ExtractAssetPaths(string html) =>
        Regex.Matches(html, "(?:src|href)=\"(?<path>/assets/[^\"]+)\"", RegexOptions.IgnoreCase)
            .Select(match => WebUtility.HtmlDecode(match.Groups["path"].Value))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string BuildMainMenuBackgroundArtifact()
    {
        return """
        <!doctype html>
        <html lang="ru" data-artifact="main-menu-background-art" data-background="enabled">
        <head>
          <meta charset="utf-8">
          <title>Browser Main Menu Background Art Visual Smoke</title>
          <style>
            :root { color-scheme: dark; font-family: Inter, "Segoe UI", sans-serif; background: #100b17; color: #f9ecd1; }
            body { margin: 0; padding: 24px; background: #100b17; }
            .artifact { display: grid; gap: 22px; max-width: 1180px; margin: 0 auto; }
            .note { border: 1px solid rgba(216, 179, 106, 0.28); border-radius: 18px; padding: 14px 18px; background: rgba(31, 24, 45, 0.86); color: rgba(249, 236, 209, 0.78); }
            .frame { position: relative; overflow: hidden; border: 1px solid rgba(216, 179, 106, 0.32); border-radius: 28px; min-height: 560px; background-image: url('../../BookOfEternityClient.WebFrontend/public/main-menu-bg.webp'); background-size: cover; background-position: center 30%; box-shadow: 0 28px 90px rgba(0, 0, 0, 0.42); }
            .frame::before { position: absolute; inset: 0; content: ''; background: linear-gradient(to bottom, rgba(6, 8, 9, 0.3), rgba(6, 8, 9, 0.85) 70%, #060809), linear-gradient(to right, rgba(6, 8, 9, 0.32), transparent 55%, rgba(6, 8, 9, 0.32)); }
            .window { position: relative; z-index: 1; display: grid; gap: 16px; max-width: 560px; margin: 48px; border: 1px solid rgba(249, 236, 209, 0.18); border-radius: 24px; padding: 24px; background: rgba(16, 12, 24, 0.72); backdrop-filter: blur(10px); }
            h1, h2, p { margin: 0; }
            h1 { color: #ffe2a6; font-size: clamp(2rem, 5vw, 4.2rem); }
            .eyebrow { color: #d8b36a; font-size: 0.78rem; font-weight: 800; letter-spacing: 0.18em; text-transform: uppercase; }
            .actions { display: grid; gap: 10px; }
            .action { border: 1px solid rgba(216, 179, 106, 0.32); border-radius: 16px; padding: 12px 14px; background: rgba(255, 255, 255, 0.06); }
            .action strong { display: block; color: #fff6df; }
            .action span { color: rgba(249, 236, 209, 0.72); }
            .narrow { width: min(100%, 390px); min-height: 640px; margin: 0 auto; }
            .narrow .window { margin: 18px; padding: 18px; }
          </style>
        </head>
        <body>
          <main class="artifact">
            <p class="note">This is a dependency-light local HTML visual smoke artifact, not an automated screenshot. It references the tracked local asset at BookOfEternityClient.WebFrontend/public/main-menu-bg.webp and keeps the main menu background enabled for readability review.</p>
            <section class="frame" data-viewport="desktop" data-background="enabled" aria-label="Desktop main menu background art smoke">
              <div class="window">
                <p class="eyebrow">главная книга</p>
                <h1>Открыть книгу</h1>
                <p>Dark-fantasy menu art stays subdued behind an overlay so menu copy and calls to action remain readable.</p>
                <div class="actions" aria-label="Действия главного меню">
                  <article class="action"><strong>Продолжить главу</strong><span>Вернуться к текущей сохранённой главе.</span></article>
                  <article class="action"><strong>Загрузить сохранение</strong><span>Выбрать локальную запись.</span></article>
                  <article class="action"><strong>Начать новую главу</strong><span>Открыть подготовку новой главы.</span></article>
                  <article class="action"><strong>Настроить книгу</strong><span>Открыть настройки книги и звука.</span></article>
                </div>
              </div>
            </section>
            <section class="frame narrow" data-viewport="narrow" data-background="enabled" aria-label="Narrow main menu background art smoke">
              <div class="window">
                <p class="eyebrow">главная книга</p>
                <h2>Открыть книгу</h2>
                <p>At narrow width the cover crop keeps the focal art behind the same dark overlay while controls stay legible.</p>
                <div class="actions">
                  <article class="action"><strong>Продолжить главу</strong><span>Primary action remains readable.</span></article>
                  <article class="action"><strong>Начать новую главу</strong><span>Secondary actions keep sufficient contrast.</span></article>
                </div>
              </div>
            </section>
          </main>
        </body>
        </html>
        """;
    }

    private static string BuildHomeLauncherHierarchyArtifact()
    {
        return """
        <!doctype html>
        <html lang="ru" data-artifact="home-launcher-hierarchy">
        <head>
          <meta charset="utf-8">
          <title>Browser Home Launcher Hierarchy Visual Smoke</title>
          <style>
            :root { color-scheme: dark; font-family: Inter, "Segoe UI", sans-serif; background: #060809; color: #f3e6c8; }
            body { margin: 0; padding: 24px; background: radial-gradient(circle at top left, rgba(201, 162, 77, 0.18), transparent 34%), #060809; }
            .artifact { display: grid; gap: 20px; max-width: 1160px; margin: 0 auto; }
            .note { border: 1px solid rgba(212, 179, 106, 0.32); border-radius: 16px; padding: 13px 16px; background: rgba(10, 14, 16, 0.88); color: rgba(243, 230, 200, 0.8); }
            .frame { position: relative; overflow: hidden; border: 1px solid rgba(201, 162, 77, 0.28); border-radius: 24px; min-height: 560px; background: radial-gradient(ellipse at 18% 18%, rgba(201, 162, 77, 0.2), transparent 36%), radial-gradient(ellipse at 78% 24%, rgba(139, 95, 212, 0.18), transparent 40%), url('../../BookOfEternityClient.WebFrontend/public/main-menu-bg.webp') center 30% / cover; box-shadow: 0 28px 90px rgba(0, 0, 0, 0.48); }
            .frame::before { position: absolute; inset: 0; content: ''; background: linear-gradient(to bottom, rgba(6, 8, 9, 0.28), rgba(6, 8, 9, 0.86) 70%, #060809), linear-gradient(to right, rgba(6, 8, 9, 0.36), transparent 56%, rgba(6, 8, 9, 0.36)); }
            .window { position: relative; z-index: 1; display: grid; gap: 16px; width: min(560px, calc(100% - 48px)); margin: 48px; border: 1px solid rgba(243, 230, 200, 0.16); border-radius: 20px; padding: 22px; background: rgba(6, 8, 9, 0.72); backdrop-filter: blur(10px); }
            h1, h2, p { margin: 0; }
            h1 { color: #f5dfa0; font-size: clamp(2rem, 5vw, 4rem); }
            .eyebrow { color: #c9a24d; font-size: 0.78rem; font-weight: 800; letter-spacing: 0.18em; text-transform: uppercase; }
            .warning { width: fit-content; border: 1px solid rgba(212, 179, 106, 0.46); border-radius: 999px; padding: 8px 12px; background: rgba(212, 179, 106, 0.13); color: #f5dfa0; font-weight: 800; }
            .actions { display: grid; gap: 10px; }
            .action { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 4px 12px; align-items: center; border: 1px solid rgba(201, 162, 77, 0.28); border-radius: 15px; padding: 12px 14px; background: linear-gradient(135deg, rgba(201, 162, 77, 0.12), rgba(8, 11, 12, 0.88)); }
            .action strong { color: #fff6df; }
            .action span { color: rgba(243, 230, 200, 0.72); }
            .affordance { grid-column: 2; grid-row: 1 / span 2; border: 1px solid rgba(235, 212, 142, 0.42); border-radius: 999px; padding: 5px 9px; color: #ebd48e; font-size: 0.72rem; font-weight: 850; text-transform: uppercase; }
            .action[data-action-state="disabled"] { opacity: 0.55; cursor: not-allowed; border-color: rgba(255, 255, 255, 0.06); background: rgba(255, 255, 255, 0.03); filter: saturate(0.55); }
            .action[data-action-state="disabled"] .affordance { color: rgba(168, 179, 171, 0.82); border-color: rgba(255, 255, 255, 0.06); }
            .narrow { width: min(100%, 390px); min-height: 640px; margin: 0 auto; }
            .narrow .window { width: auto; margin: 18px; padding: 18px; }
          </style>
        </head>
        <body>
          <main class="artifact">
            <p class="note">This is a dependency-light local HTML visual smoke artifact, not an automated screenshot. It checks Home launcher action hierarchy, disabled reasons, validation warning treatment, and the local ambient art fallback.</p>
            <section class="frame" data-viewport="desktop" aria-label="Desktop Home launcher hierarchy">
              <div class="window">
                <p class="eyebrow">главная книга</p>
                <h1>Открыть книгу</h1>
                <p>Выберите продолжение, загрузку или новую главу.</p>
                <p class="warning" data-validation="warning">Сессия читается, но валидация обнаружила ошибки: 9</p>
                <div class="actions" aria-label="Действия главного меню">
                  <article class="action" data-action-state="enabled"><strong>Продолжить главу</strong><span>Вернуться к текущей сохранённой главе.</span><span class="affordance">Открыть →</span></article>
                  <article class="action" data-action-state="disabled"><strong>Загрузить сохранение</strong><span>Сохранений пока нет. Когда книга найдёт записи, они появятся здесь.</span><span class="affordance">Закрыто</span></article>
                  <article class="action" data-action-state="disabled"><strong>Начать новую главу</strong><span>Подготовка новой главы пока недоступна из главного меню.</span><span class="affordance">Закрыто</span></article>
                </div>
              </div>
            </section>
            <section class="frame narrow" data-viewport="narrow" aria-label="Narrow Home launcher hierarchy">
              <div class="window">
                <p class="eyebrow">главная книга</p>
                <h2>Открыть книгу</h2>
                <p class="warning" data-validation="warning">Проверка книги требует внимания.</p>
                <div class="actions">
                  <article class="action" data-action-state="enabled"><strong>Тренировка QTE</strong><span>Свободная тренировка быстрых сцен без наград.</span><span class="affordance">Открыть →</span></article>
                  <article class="action" data-action-state="disabled"><strong>Начать новую главу</strong><span>Сначала завершите текущий безопасный шаг книги.</span><span class="affordance">Закрыто</span></article>
                </div>
              </div>
            </section>
          </main>
        </body>
        </html>
        """;
    }

    private static string BuildFirstScreenVisualQaArtifact(string appSource, string tabBarConfigSource, string launcherSource)
    {
        var tabs = ExtractPlayerTabs(tabBarConfigSource);
        var tabSequence = string.Join(" → ", tabs.Select(tab => tab.Label));

        Assert.Equal(new[] { "scene", "status", "help", "settings" }, tabs.Select(tab => tab.Id));
        Assert.Contains("<GameLauncher menu={menu} />", appSource, StringComparison.Ordinal);
        Assert.Contains("Книга Вечности: Перерождение", launcherSource, StringComparison.Ordinal);
        Assert.Contains("Открыть книгу", launcherSource, StringComparison.Ordinal);
        Assert.Contains("Продолжить главу", launcherSource, StringComparison.Ordinal);
        Assert.Contains("Загрузить сохранение", launcherSource, StringComparison.Ordinal);
        Assert.Contains("Настроить книгу", launcherSource, StringComparison.Ordinal);
        Assert.DoesNotContain("<h1 id=\"browser-client-title\">Локальный игровой клиент</h1>", launcherSource, StringComparison.Ordinal);

        return $$"""
        <!doctype html>
        <html lang="ru" data-artifact="browser-first-screen-visual-qa">
        <head>
          <meta charset="utf-8">
          <title>Browser Client First Screen Visual QA</title>
          <style>
            :root { color-scheme: dark; font-family: Inter, "Segoe UI", sans-serif; background: #100b17; color: #f9ecd1; }
            body { margin: 0; padding: 24px; background: radial-gradient(circle at top left, rgba(216, 179, 106, 0.2), transparent 32%), #100b17; }
            .artifact { display: grid; gap: 20px; max-width: 1180px; margin: 0 auto; }
            .frame { border: 1px solid rgba(249, 236, 209, 0.18); border-radius: 28px; background: rgba(31, 24, 45, 0.88); box-shadow: 0 24px 80px rgba(0, 0, 0, 0.34); overflow: hidden; }
            .desktop-shell { display: grid; grid-template-columns: 280px 1fr 280px; min-height: 560px; }
            .mobile-shell { width: min(100%, 390px); margin: 0 auto; }
            .sidebar, .status, .mobile-nav { padding: 18px; background: rgba(16, 12, 24, 0.74); }
            .content { padding: 28px; display: grid; gap: 18px; align-content: start; }
            .brand { color: #ffe2a6; letter-spacing: 0.04em; }
            .primary { border: 1px solid rgba(216, 179, 106, 0.5); border-radius: 22px; padding: 18px; background: linear-gradient(135deg, rgba(216, 179, 106, 0.24), rgba(155, 107, 255, 0.12)); }
            .secondary, .route-card, .check { border: 1px solid rgba(216, 179, 106, 0.24); border-radius: 18px; padding: 12px; background: rgba(255, 255, 255, 0.055); }
            .route-list, .checks, .secondary-row { display: grid; gap: 10px; }
            .route-card strong { display: flex; align-items: center; gap: 8px; }
            .route-card__mark { width: 14px; height: 14px; border-radius: 50%; border: 1px solid rgba(216, 179, 106, 0.65); box-shadow: inset 0 0 0 3px rgba(216, 179, 106, 0.18); }
            .secondary-row { grid-template-columns: repeat(3, minmax(0, 1fr)); }
            .muted { color: rgba(249, 236, 209, 0.72); }
            .locked { color: rgba(249, 236, 209, 0.62); border-style: dashed; }
            .advanced { margin-top: 16px; color: rgba(249, 236, 209, 0.62); border: 1px dashed rgba(249, 236, 209, 0.24); border-radius: 16px; padding: 12px; }
            @media (max-width: 860px) { .desktop-shell, .secondary-row { grid-template-columns: 1fr; } }
          </style>
        </head>
        <body>
          <main class="artifact">
            <section class="frame" data-viewport="desktop" data-state="fresh-empty" aria-label="Desktop first-screen visual QA">
              <div class="desktop-shell">
                <nav class="sidebar" aria-label="Player tabs">
                  <p class="brand">{{WebUtility.HtmlEncode(tabSequence)}}</p>
                  <div class="route-list">
        {{RenderVisualQaRouteCards(tabs)}}
                  </div>
                  <div class="advanced">advanced debug secondary: Расширенный режим остаётся отдельным вторичным входом.</div>
                </nav>
                <section class="content" aria-label="Launcher visual target">
                  <p class="brand">Книга Вечности: Перерождение</p>
                  <h1>Открыть книгу</h1>
                  <p class="muted">Default first screen reads as a game launcher, not a local runtime dashboard.</p>
                  <article class="primary"><strong>Primary CTA: Продолжить главу</strong><p>Если продолжение недоступно, CTA переключается на Загрузить сохранение или Начать новую главу.</p></article>
                  <div class="secondary-row">
                    <article class="secondary">Загрузить сохранение</article>
                    <article class="secondary">Начать новую главу</article>
                    <article class="secondary">Настроить книгу</article>
                  </div>
                  <article class="secondary locked">Обычная no-session пауза выглядит приглушённо, без красных повторяющихся unavailable alerts.</article>
                </section>
                <aside class="status" aria-label="Player status rail">
                  <h2>Сводка книги</h2>
                  <p class="muted">Слой книги · Герой и душа · Сохранение · Активной главы пока нет.</p>
                  <div class="checks">
                    <div class="check">current minimal tab shell: launcher, scene, status, help, settings, single command input.</div>
                    <div class="check">no technical hero copy</div>
                    <div class="check">no repeated unavailable alerts</div>
                    <div class="check">no emoji route icons</div>
                  </div>
                </aside>
              </div>
            </section>
            <section class="frame mobile-shell" data-viewport="mobile" aria-label="Mobile first-screen visual QA">
              <div class="mobile-nav">
                <p class="brand">Книга Вечности: Перерождение</p>
                <h1>Открыть книгу</h1>
                <article class="primary">Primary CTA: Продолжить главу</article>
                <p class="muted">{{WebUtility.HtmlEncode(tabSequence)}}</p>
                <div class="route-list">
        {{RenderVisualQaRouteCards(tabs)}}
                </div>
                <div class="advanced">advanced debug secondary</div>
              </div>
            </section>
          </main>
        </body>
        </html>
        """;
    }

    private static string BuildStartNewChapterFlowArtifact(string launcherSource, string promptFormSource)
    {
        Assert.Contains("function NewChapterStartPanel", launcherSource, StringComparison.Ordinal);
        Assert.Contains("Форма новой главы", launcherSource, StringComparison.Ordinal);
        Assert.Contains("browserApi.executeExplorerCommand({ command: startCommand", launcherSource, StringComparison.Ordinal);
        Assert.Contains("browserApi.submitPromptSession", launcherSource, StringComparison.Ordinal);
        Assert.Contains("renderPromptControl", promptFormSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Подготовить новую историю через управляемую форму браузера.", launcherSource, StringComparison.Ordinal);

        return """
        <!doctype html>
        <html lang="ru" data-artifact="browser-start-new-chapter-flow">
        <head>
          <meta charset="utf-8">
          <title>Browser Start New Chapter Flow Visual Smoke</title>
          <style>
            :root { color-scheme: dark; font-family: Inter, "Segoe UI", sans-serif; background: #100b17; color: #f9ecd1; }
            body { margin: 0; padding: 24px; background: radial-gradient(circle at top left, rgba(216, 179, 106, 0.2), transparent 32%), #100b17; }
            .artifact { display: grid; gap: 20px; max-width: 1120px; margin: 0 auto; }
            .frame { border: 1px solid rgba(249, 236, 209, 0.18); border-radius: 26px; background: rgba(31, 24, 45, 0.9); box-shadow: 0 24px 80px rgba(0, 0, 0, 0.34); padding: 24px; }
            .desktop { display: grid; grid-template-columns: 1fr 1.15fr; gap: 18px; }
            .mobile { width: min(100%, 390px); margin: 0 auto; }
            .panel, .form, .unavailable { border: 1px solid rgba(216, 179, 106, 0.28); border-radius: 18px; padding: 16px; background: rgba(255, 255, 255, 0.055); }
            .form { display: grid; gap: 12px; }
            label { display: grid; gap: 6px; color: #ffe9b8; }
            input, textarea, select { border: 1px solid rgba(216, 179, 106, 0.32); border-radius: 12px; padding: 10px; background: rgba(0,0,0,0.28); color: #f9ecd1; }
            button { border: 1px solid rgba(216, 179, 106, 0.52); border-radius: 14px; padding: 12px 16px; background: rgba(216, 179, 106, 0.2); color: #fff6df; font-weight: 800; }
            .muted { color: rgba(249, 236, 209, 0.7); }
            .unavailable { border-style: dashed; color: rgba(249, 236, 209, 0.78); }
            @media (max-width: 760px) { .desktop { grid-template-columns: 1fr; } }
          </style>
        </head>
        <body>
          <main class="artifact">
            <section class="frame desktop" data-viewport="desktop" aria-label="Desktop start-new-chapter flow">
              <article class="panel">
                <p class="muted">Главная книга · игрок выбирает действие</p>
                <h1>Начать новую главу</h1>
                <p>Кнопка открывает форму новой главы только когда локальная книга отдаёт доступный безопасный поток.</p>
                <button type="button">Открыть форму новой главы</button>
                <div class="unavailable">truthful unavailable state: если локальная запись заблокирована или команда отсутствует, игрок видит причину и путь — продолжить главу, загрузить сохранение или проверить состояние книги.</div>
              </article>
              <article class="form" aria-label="Форма новой главы">
                <h2>Форма новой главы</h2>
                <label>Режим подготовки мира<select><option>Создать / редактировать</option><option>Применить профиль</option><option>Очистить</option></select></label>
                <label>Название мира<input value="Королевство пепельных колоколов" readonly></label>
                <label>Директивы мира<textarea rows="4" readonly>Опишите жанр, запреты, обязательные темы, стартовые обстоятельства и роль персонажа.</textarea></label>
                <button type="button">Отправить форму</button>
              </article>
            </section>
            <section class="frame mobile" data-viewport="mobile" aria-label="Mobile start-new-chapter flow">
              <h1>Начать новую главу</h1>
              <p class="muted">Форма новой главы остаётся внутри главной книги и не раскрывает технические команды.</p>
              <div class="form">
                <label>Режим подготовки мира<select><option>Создать / редактировать</option></select></label>
                <label>Название мира<input value="Новый мир" readonly></label>
                <button type="button">Отправить форму</button>
              </div>
            </section>
          </main>
        </body>
        </html>
        """;
    }

    private static string BuildBrowserImagegenAssetsArtifact(
        string assetModuleSource,
        string sceneHeroSource,
        string cinematicSceneHeroSource,
        string sceneViewSource,
        string blockRendererSource,
        string statusViewSource)
    {
        Assert.Contains("sceneHeroFallback", assetModuleSource, StringComparison.Ordinal);
        Assert.Contains("galleryEmptyArchive", assetModuleSource, StringComparison.Ordinal);
        Assert.Contains("statusSoulVignette", assetModuleSource, StringComparison.Ordinal);
        Assert.Contains("fallbackImageUrl", sceneHeroSource, StringComparison.Ordinal);
        Assert.Contains("CinematicSceneHero", sceneHeroSource, StringComparison.Ordinal);
        Assert.Contains("event.currentTarget.hidden = true;", cinematicSceneHeroSource, StringComparison.Ordinal);
        Assert.Contains("fallbackImageUrl={browserUiAssets.sceneHeroFallback.url}", sceneViewSource, StringComparison.Ordinal);
        Assert.Contains("browserUiAssets.galleryEmptyArchive.url", blockRendererSource, StringComparison.Ordinal);
        Assert.Contains("block-image--fallback", blockRendererSource, StringComparison.Ordinal);
        Assert.Contains("browserUiAssets.statusSoulVignette.url", statusViewSource, StringComparison.Ordinal);
        Assert.Contains("status-view__ambient-art", statusViewSource, StringComparison.Ordinal);

        return """
        <!doctype html>
        <html lang="ru" data-artifact="browser-imagegen-assets">
        <head>
          <meta charset="utf-8">
          <title>Browser Image Asset Visual Smoke</title>
          <style>
            :root { color-scheme: dark; font-family: Inter, "Segoe UI", sans-serif; background: #060809; color: #f4e7c9; }
            body { margin: 0; padding: 24px; background: radial-gradient(circle at 12% 8%, rgba(201, 162, 77, 0.18), transparent 32%), #060809; }
            .artifact { display: grid; gap: 22px; max-width: 1180px; margin: 0 auto; }
            .note { border: 1px solid rgba(212, 179, 106, 0.32); border-radius: 16px; padding: 13px 16px; background: rgba(10, 14, 16, 0.88); color: rgba(243, 230, 200, 0.82); line-height: 1.55; }
            .frame { overflow: hidden; border: 1px solid rgba(201, 162, 77, 0.28); border-radius: 24px; background: rgba(14, 19, 20, 0.88); box-shadow: 0 28px 90px rgba(0, 0, 0, 0.46); }
            .scene { position: relative; min-height: 430px; background-image: linear-gradient(to top, #060809 6%, rgba(6, 8, 9, 0.88) 30%, rgba(6, 8, 9, 0.18)), linear-gradient(to right, rgba(6, 8, 9, 0.62), rgba(6, 8, 9, 0.08) 62%), url('../../BookOfEternityClient.WebFrontend/public/browser-ui-assets/scene-hero-fallback.png'); background-size: cover; background-position: center 32%; }
            .scene-copy { position: absolute; left: 32px; right: 32px; bottom: 28px; max-width: 620px; display: grid; gap: 8px; }
            .eyebrow { margin: 0; color: #c9a24d; font-size: 0.76rem; font-weight: 850; letter-spacing: 0.18em; text-transform: uppercase; }
            h1, h2, p { margin: 0; }
            h1 { color: #f5dfa0; font-size: clamp(1.8rem, 4vw, 3.6rem); line-height: 1.05; text-shadow: 0 3px 26px rgba(0, 0, 0, 0.85); }
            .muted { color: rgba(244, 231, 201, 0.78); line-height: 1.55; }
            .grid { display: grid; grid-template-columns: minmax(0, 1.15fr) minmax(260px, 0.85fr); gap: 18px; padding: 20px; }
            .gallery { display: grid; gap: 10px; padding: 14px; border: 1px solid rgba(201, 162, 77, 0.24); border-radius: 18px; background: rgba(255, 255, 255, 0.045); }
            .gallery img { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; border-radius: 12px; opacity: 0.76; filter: saturate(0.7) brightness(0.72); }
            .status { position: relative; min-height: 360px; padding: 18px; display: grid; gap: 12px; align-content: start; }
            .status::before { position: absolute; inset: -80px -80px auto auto; width: 430px; aspect-ratio: 1 / 1; content: ""; background-image: url('../../BookOfEternityClient.WebFrontend/public/browser-ui-assets/status-soul-vignette.png'); background-size: cover; opacity: 0.28; mix-blend-mode: screen; }
            .status-card { position: relative; z-index: 1; border: 1px solid rgba(201, 162, 77, 0.22); border-radius: 16px; padding: 12px; background: rgba(8, 11, 12, 0.82); }
            .status-card strong { color: #fff6df; }
            .mobile { width: min(100%, 390px); margin: 0 auto; }
            .mobile .scene { min-height: 560px; background-position: center 32%; }
            .mobile .scene-copy { left: 18px; right: 18px; bottom: 22px; }
            .mobile .grid { grid-template-columns: 1fr; }
            @media (max-width: 760px) { .grid { grid-template-columns: 1fr; } }
          </style>
        </head>
        <body>
          <main class="artifact">
            <p class="note">This is a dependency-light local visual-smoke artifact, not an automated screenshot. It references committed local assets from BookOfEternityClient.WebFrontend/public/browser-ui-assets and checks desktop/mobile crop, fallback framing, and text readability without network calls.</p>
            <section class="frame scene" data-viewport="desktop" data-state="scene-fallback" aria-label="Desktop scene fallback art">
              <div class="scene-copy">
                <p class="eyebrow">ход 12</p>
                <h1>Мир смертных</h1>
                <p class="muted">Когда образ сцены ещё не пришёл из главы, локальная подложка остаётся спокойной и не спорит с названием, местом и временем.</p>
              </div>
            </section>
            <section class="frame mobile" data-viewport="mobile" data-state="scene-fallback" aria-label="Mobile scene fallback art">
              <div class="scene">
                <div class="scene-copy">
                  <p class="eyebrow">ход 12</p>
                  <h1>Мир смертных</h1>
                  <p class="muted">Узкий кадр сохраняет тёмную нижнюю зону для текста.</p>
                </div>
              </div>
            </section>
            <section class="frame" data-viewport="desktop" aria-label="Gallery and status asset smoke">
              <div class="grid">
                <article class="gallery" data-state="gallery-empty">
                  <img src="../../BookOfEternityClient.WebFrontend/public/browser-ui-assets/gallery-empty-archive.png" alt="">
                  <h2>Галерея ждёт образ</h2>
                  <p class="muted">Подпись находится вне изображения, поэтому архивная пыль не мешает чтению.</p>
                </article>
                <article class="status" data-state="status-ambient">
                  <div class="status-card"><strong>Персонаж</strong><p class="muted">Имя, класс и состояние читаются на отдельной карточке.</p></div>
                  <div class="status-card"><strong>Душа</strong><p class="muted">Декоративный знак остаётся за карточками и не перекрывает сведения.</p></div>
                  <div class="status-card"><strong>Посмертие</strong><p class="muted">Сияние, искры и залы остаются игровыми счётчиками.</p></div>
                </article>
              </div>
            </section>
            <section class="frame mobile" data-viewport="mobile" aria-label="Mobile gallery and status asset smoke">
              <div class="grid">
                <article class="gallery" data-state="gallery-empty">
                  <img src="../../BookOfEternityClient.WebFrontend/public/browser-ui-assets/gallery-empty-archive.png" alt="">
                  <h2>Образ пока не проявился</h2>
                  <p class="muted">Фрейм остаётся понятным в узком окне.</p>
                </article>
                <article class="status" data-state="status-ambient">
                  <div class="status-card"><strong>Душа</strong><p class="muted">Карточка перекрывает декоративный фон.</p></div>
                </article>
              </div>
            </section>
          </main>
        </body>
        </html>
        """;
    }

    private static string BuildRebornPanelsArtifact(string statusViewSource)
    {
        Assert.Contains("<h3>✨ Посмертие</h3>", statusViewSource, StringComparison.Ordinal);
        Assert.Contains("Сияние", statusViewSource, StringComparison.Ordinal);
        Assert.Contains("Искры света", statusViewSource, StringComparison.Ordinal);
        Assert.Contains("Залы", statusViewSource, StringComparison.Ordinal);
        Assert.Contains("Фракции", statusViewSource, StringComparison.Ordinal);

        return """
        <!doctype html>
        <html lang="ru" data-artifact="browser-reborn-panels">
        <head><meta charset="utf-8"><title>Browser Reborn Panels Visual Smoke</title></head>
        <body>
          <main>
            <section data-viewport="desktop" data-state="mortal-locked">
              <h1>Посмертие Reborn</h1>
              <article><strong>🕯️ Afterlife</strong><p>Посмертные панели откроются, когда душа перейдёт в посмертие.</p></article>
              <article><strong>✦ Сияющая Обитель</strong><p>Доступ к Обители появится после перехода в посмертный слой.</p></article>
              <article><strong>🌊 Море Хаоса</strong><p>Навигация Моря Хаоса ждёт подходящего царства.</p></article>
            </section>
            <section data-viewport="desktop" data-state="afterlife-active">
              <h1>Посмертие Reborn</h1>
              <article><strong>Afterlife</strong><p>Душа в посмертии · перья и просветление видны игроку.</p></article>
              <article><strong>Сияющая Обитель</strong><p>Сияние, искры света, залы и безопасные действия.</p></article>
              <article><strong>Море Хаоса</strong><p>Статус моря, ориентиры и доступные игровые действия.</p></article>
            </section>
            <section data-viewport="mobile" data-state="afterlife-active">
              <h1>Мобильный вид: Посмертие Reborn</h1>
              <p>Afterlife → Сияющая Обитель → Море Хаоса</p>
            </section>
          </main>
        </body>
        </html>
        """;
    }

    private static string BuildDetailSurfaceArtifact(string statusViewSource)
    {
        Assert.Contains("<h3>🎭 Персонаж</h3>", statusViewSource, StringComparison.Ordinal);
        Assert.Contains("<h3>🕯️ Душа</h3>", statusViewSource, StringComparison.Ordinal);
        Assert.Contains("<h3>🗺️ Мир</h3>", statusViewSource, StringComparison.Ordinal);
        Assert.Contains("<h3>✨ Посмертие</h3>", statusViewSource, StringComparison.Ordinal);
        Assert.Contains("function StatusMeter", statusViewSource, StringComparison.Ordinal);
        Assert.Contains("className={`status-meter status-meter--${severity}`}", statusViewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("className=\"status-bar\"", statusViewSource, StringComparison.Ordinal);

        return """
        <!doctype html>
        <html lang="ru" data-artifact="browser-detail-surfaces">
        <head>
          <meta charset="utf-8">
          <title>Browser Client Detail Surfaces Visual Smoke</title>
          <style>
            :root { color-scheme: dark; font-family: Inter, "Segoe UI", sans-serif; background: #100b17; color: #f9ecd1; }
            body { margin: 0; padding: 24px; background: radial-gradient(circle at top left, rgba(216, 179, 106, 0.2), transparent 32%), #100b17; }
            .artifact { display: grid; gap: 20px; max-width: 1180px; margin: 0 auto; }
            .frame { border: 1px solid rgba(249, 236, 209, 0.18); border-radius: 26px; background: rgba(31, 24, 45, 0.88); box-shadow: 0 24px 80px rgba(0, 0, 0, 0.34); overflow: hidden; }
            .frame header { padding: 18px 22px; border-bottom: 1px solid rgba(249, 236, 209, 0.12); }
            .cards { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; padding: 22px; }
            .card, .modal, .section { border: 1px solid rgba(216, 179, 106, 0.28); border-radius: 18px; background: rgba(255, 255, 255, 0.055); padding: 16px; }
            .card strong, .modal h2, .section h3 { color: #ffe2a6; }
            .card p, .section p { color: rgba(249, 236, 209, 0.76); line-height: 1.5; }
            .modal-wrap { padding: 22px; background: rgba(0, 0, 0, 0.28); }
            .modal { display: grid; gap: 14px; max-width: 760px; margin: 0 auto; }
            .modal-bar { display: flex; justify-content: space-between; gap: 12px; border-bottom: 1px solid rgba(249, 236, 209, 0.12); padding-bottom: 12px; }
            .controls { display: flex; gap: 8px; flex-wrap: wrap; }
            .controls span { border: 1px solid rgba(249, 236, 209, 0.2); border-radius: 999px; padding: 6px 10px; color: #fff6df; }
            .sections { display: grid; gap: 12px; }
            .mobile { width: min(100%, 420px); margin: 0 auto; }
            .mobile .modal { min-height: 520px; border-radius: 0; }
            .sequence { color: #d8b36a; font-weight: 700; }
            @media (max-width: 720px) { .cards { grid-template-columns: 1fr; } }
          </style>
        </head>
        <body>
          <main class="artifact">
            <section class="frame" data-viewport="desktop" data-state="status-overview" aria-label="Compact status overview">
              <header>
                <h1>Status overview: player data stays compact</h1>
                <p class="sequence">Персонаж → Душа → Мир</p>
              </header>
              <div class="cards">
                <article class="card"><strong>Персонаж</strong><p>Имя, класс, раса и состояние читаются как игровые сведения.</p></article>
                <article class="card"><strong>Душа</strong><p>Имя души, царство, инкарнация и чернильные перья.</p></article>
                <article class="card"><strong>Мир</strong><p>Локация, время и номер хода без технических путей.</p></article>
              </div>
            </section>
            <section class="frame" data-viewport="desktop" data-state="afterlife-available" aria-label="Afterlife status card">
              <header><h1>Посмертие</h1></header>
              <div class="modal-wrap">
                <article class="modal">
                  <div class="modal-bar">
                    <div><p class="sequence">посмертный прогресс</p><h2>Посмертие</h2></div>
                    <div class="controls"><span>Сияние</span><span>Искры света</span><span>Залы</span></div>
                  </div>
                  <p>Эта панель показывает текущую игровую сводку посмертия без служебных pending/control путей.</p>
                  <div class="sections">
                    <section class="section"><h3>Сияние</h3><p>Опыт, уровень и искры света сгруппированы в понятный раздел.</p></section>
                    <section class="section"><h3>Залы</h3><p>Залы и фракции видны только как игровые счетчики.</p></section>
                  </div>
                </article>
              </div>
            </section>
            <section class="frame mobile" data-viewport="mobile" data-state="status-overview" aria-label="Mobile status surface">
              <header><h1>Mobile status surface</h1></header>
              <article class="modal">
                <div class="modal-bar">
                  <div><p class="sequence">статус</p><h2>Персонаж</h2></div>
                  <div class="controls"><span>Душа</span><span>Мир</span></div>
                </div>
                <section class="section"><h3>Состояние</h3><p>Здоровье, энергия и стойкость остаются читаемыми в узком окне.</p></section>
                <section class="section"><h3>Локация</h3><p>Игровая локация и ход сохраняют плотный обзор без служебных данных.</p></section>
              </article>
            </section>
          </main>
        </body>
        </html>
        """;
    }

    private static string BuildNavigationIaArtifact(string tabBarConfigSource)
    {
        var tabs = ExtractPlayerTabs(tabBarConfigSource);
        var tabSequence = string.Join(" → ", tabs.Select(tab => tab.Label));

        Assert.Equal(new[] { "scene", "status", "help", "settings" }, tabs.Select(tab => tab.Id));

        return $$"""
        <!doctype html>
        <html lang="ru" data-artifact="browser-navigation-ia">
        <head>
          <meta charset="utf-8">
          <title>Browser Client Navigation IA Visual Smoke</title>
          <style>
            :root { color-scheme: dark; font-family: Inter, "Segoe UI", sans-serif; background: #130f1d; color: #f9ecd1; }
            body { margin: 0; padding: 24px; background: radial-gradient(circle at top left, rgba(155, 107, 255, 0.28), transparent 34%), #130f1d; }
            .artifact { display: grid; gap: 20px; max-width: 1180px; margin: 0 auto; }
            .frame { border: 1px solid rgba(249, 236, 209, 0.2); border-radius: 26px; background: rgba(31, 24, 45, 0.88); box-shadow: 0 24px 80px rgba(0, 0, 0, 0.34); overflow: hidden; }
            .frame header { padding: 18px 22px; border-bottom: 1px solid rgba(249, 236, 209, 0.12); }
            .desktop-shell { display: grid; grid-template-columns: 280px 1fr; min-height: 520px; }
            .sidebar, .mobile-nav { padding: 18px; background: rgba(16, 12, 24, 0.74); }
            .content { padding: 24px; display: grid; gap: 16px; align-content: start; }
            .route-list { display: grid; gap: 10px; }
            .route-card { border: 1px solid rgba(216, 179, 106, 0.22); border-radius: 18px; padding: 12px; background: rgba(255, 255, 255, 0.055); }
            .route-card strong { display: flex; gap: 8px; align-items: center; color: #ffe2a6; }
            .route-card p { margin: 6px 0 0; color: rgba(249, 236, 209, 0.76); font-size: 0.92rem; line-height: 1.45; }
            .utility { margin-top: 18px; opacity: 0.82; }
            .advanced-note { border-radius: 18px; border: 1px dashed rgba(249, 236, 209, 0.26); padding: 14px; color: rgba(249, 236, 209, 0.7); }
            .mobile-shell { width: min(100%, 420px); margin: 0 auto; }
            .mobile-nav .route-list { grid-template-columns: repeat(2, minmax(0, 1fr)); }
            .sequence { color: #d8b36a; font-weight: 700; }
          </style>
        </head>
        <body>
          <main class="artifact">
            <section class="frame" data-viewport="desktop" aria-label="Desktop browser navigation smoke">
              <header>
                <h1>Desktop: player navigation taxonomy</h1>
                <p class="sequence">{{WebUtility.HtmlEncode(tabSequence)}}</p>
              </header>
              <div class="desktop-shell">
                <nav class="sidebar" aria-label="Основные игровые разделы книги">
                  <div class="route-list">
        {{RenderRouteCards(tabs)}}
                  </div>
                </nav>
                <section class="content" aria-label="Игровая область">
                  <h2>Откройте книгу</h2>
                  <p>Обычное пустое состояние выглядит как игровая пауза: выберите главу, продолжите сохранение или перейдите к сцене, статусу, помощи и настройкам.</p>
                  <p class="advanced-note">Расширенный режим скрыт до явного включения и визуально вторичен.</p>
                </section>
              </div>
            </section>
            <section class="frame mobile-shell" data-viewport="mobile" aria-label="Mobile browser navigation smoke">
              <header>
                <h1>Mobile: compact player navigation</h1>
                <p class="sequence">{{WebUtility.HtmlEncode(tabSequence)}}</p>
              </header>
              <nav class="mobile-nav" aria-label="Мобильные игровые разделы книги">
                <div class="route-list">
        {{RenderRouteCards(tabs)}}
                </div>
                <div class="advanced-note">Расширенный режим остаётся отдельным вторичным переключателем.</div>
              </nav>
            </section>
          </main>
        </body>
        </html>
        """;
    }

    private static BrowserNavigationTab[] ExtractPlayerTabs(string tabBarConfigSource)
    {
        var tabsMatch = Regex.Match(
            tabBarConfigSource,
            @"export const tabNav: readonly TabNavItem\[\] = \[(?<tabs>.*?)\];",
            RegexOptions.Singleline);
        Assert.True(tabsMatch.Success, "tabBarConfig.ts should define tabNav metadata for the visual smoke artifact.");

        var tabMatches = Regex.Matches(
            tabsMatch.Groups["tabs"].Value,
            @"\{\s*id:\s*'(?<id>[^']+)',\s*glyph:\s*'(?<glyph>[^']+)',\s*label:\s*'(?<label>[^']+)',\s*shortcut:\s*'(?<shortcut>[^']+)',\s*description:\s*'(?<description>[^']+)'\s*\}",
            RegexOptions.Singleline);

        var tabs = tabMatches
            .Select(match => new BrowserNavigationTab(
                match.Groups["id"].Value,
                match.Groups["label"].Value,
                match.Groups["shortcut"].Value,
                match.Groups["description"].Value))
            .ToArray();
        Assert.Equal(4, tabs.Length);
        return tabs;
    }

    private static string RenderRouteCards(IEnumerable<BrowserNavigationTab> tabs) => string.Join(
        Environment.NewLine,
        tabs.Select(tab =>
            $"""
                    <article class="route-card route-card--{WebUtility.HtmlEncode(tab.Id)}">
                      <strong><span class="route-card__mark" aria-hidden="true">{WebUtility.HtmlEncode(tab.Shortcut)}</span>{WebUtility.HtmlEncode(tab.Label)}</strong>
                      <p>{WebUtility.HtmlEncode(tab.Description)}</p>
                    </article>
            """));

    private static string RenderVisualQaRouteCards(IEnumerable<BrowserNavigationTab> tabs) => string.Join(
        Environment.NewLine,
        tabs.Select(tab =>
            $"""
                    <article class="route-card route-card--{WebUtility.HtmlEncode(tab.Id)}">
                      <strong><span class="route-card__mark" aria-hidden="true">{WebUtility.HtmlEncode(tab.Shortcut)}</span>{WebUtility.HtmlEncode(tab.Label)}</strong>
                      <p>{WebUtility.HtmlEncode(tab.Description)}</p>
                    </article>
            """));

    private sealed record BrowserNavigationTab(string Id, string Label, string Shortcut, string Description);

    private sealed record SmokeResponse(string Path, HttpStatusCode StatusCode, string? ContentType, string Body)
    {
        public SmokeRequestArtifact ToArtifact() => new(Path, (int)StatusCode, ContentType, Body.Length);
    }

    private sealed record SmokeRequestArtifact(string Path, int Status, string? ContentType, int BodyLength);

    private static string PrepareArtifactDirectory()
    {
        var artifactRoot = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "browser-smoke");
        Directory.CreateDirectory(artifactRoot);
        return artifactRoot;
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

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
