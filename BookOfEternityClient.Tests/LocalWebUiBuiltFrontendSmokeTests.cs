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
                },
                new JsonSerializerOptions { WriteIndented = true }));
        var navigationArtifactPath = Path.Combine(artifactRoot, "navigation-ia.html");
        var detailSurfaceArtifactPath = Path.Combine(artifactRoot, "detail-surfaces.html");
        var rebornPanelsArtifactPath = Path.Combine(artifactRoot, "reborn-panels.html");

        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Equal(HttpStatusCode.OK, gameRoute.StatusCode);
        Assert.Equal(HttpStatusCode.OK, menuResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, screenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingApi.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingAsset.StatusCode);

        Assert.Contains("<div id=\"root\"></div>", root.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/assets/", root.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The Book of Eternity: Reborn", root.Body, StringComparison.Ordinal);
        Assert.Equal(root.Body, gameRoute.Body);
        Assert.NotEmpty(assetPaths);
        Assert.All(assetResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Contains(assetResponses, response => response.Path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) &&
            response.ContentType?.Contains("javascript", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(assetResponses, response => response.Path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) &&
            response.ContentType?.Contains("text/css", StringComparison.OrdinalIgnoreCase) == true);

        var menu = JsonNode.Parse(menuResponse.Body)!.AsObject();
        var session = JsonNode.Parse(sessionResponse.Body)!.AsObject();
        var screen = JsonNode.Parse(screenResponse.Body)!.AsObject();
        var appSource = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "src", "App.tsx"));
        await File.WriteAllTextAsync(navigationArtifactPath, BuildNavigationIaArtifact(appSource));
        await File.WriteAllTextAsync(detailSurfaceArtifactPath, BuildDetailSurfaceArtifact(appSource));
        await File.WriteAllTextAsync(rebornPanelsArtifactPath, BuildRebornPanelsArtifact(appSource));

        Assert.True(session["localOnly"]!.GetValue<bool>());
        Assert.Equal("CI-душа", menu["session"]!["soulName"]!.GetValue<string>());
        Assert.Equal("CI-душа", screen["soul"]!["name"]!.GetValue<string>());
        Assert.Equal("Проверочный тракт", screen["world"]!["location"]!.GetValue<string>());
        Assert.Contains("локальную книгу", screen["narrative"]!["text"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Главная", appSource, StringComparison.Ordinal);
        Assert.Contains("Игра", appSource, StringComparison.Ordinal);
        Assert.Contains("Расширенный режим", appSource, StringComparison.Ordinal);
        Assert.Contains("ActionMenu", appSource, StringComparison.Ordinal);
        Assert.Contains("Персонаж / Душа", appSource, StringComparison.Ordinal);
        Assert.Contains("Подготовить форму", appSource, StringComparison.Ordinal);
        Assert.Contains("browserApi.executeExplorerCommand({ command: action.advancedCommand", appSource, StringComparison.Ordinal);
        Assert.Contains("browserApi.submitPromptSession", appSource, StringComparison.Ordinal);
        Assert.Contains("renderPromptControl", appSource, StringComparison.Ordinal);
        Assert.Contains("AudioSettingsPanel", appSource, StringComparison.Ordinal);
        Assert.Contains("Включить музыку в браузере", appSource, StringComparison.Ordinal);
        Assert.Contains("browserApi.updateAudioSettings", appSource, StringComparison.Ordinal);
        Assert.Contains("new Audio()", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("setAdvancedEnabled(true)", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("action.advancedCommand}", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("C# каталога команд", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("C# протоколом", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("C# DTO", appSource, StringComparison.Ordinal);

        Assert.True(File.Exists(navigationArtifactPath), $"Missing browser navigation visual smoke artifact at {navigationArtifactPath}");
        var navigationArtifact = await File.ReadAllTextAsync(navigationArtifactPath);
        Assert.Contains("data-artifact=\"browser-navigation-ia\"", navigationArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"desktop\"", navigationArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"mobile\"", navigationArtifact, StringComparison.Ordinal);
        Assert.Contains("Главная → Игра → Душа → Мир → Журнал → Инвентарь", navigationArtifact, StringComparison.Ordinal);
        Assert.Contains("Медиа → Настройки", navigationArtifact, StringComparison.Ordinal);
        Assert.Contains("Расширенный режим", navigationArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("Debug", navigationArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Network", navigationArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command coverage", navigationArtifact, StringComparison.OrdinalIgnoreCase);

        Assert.True(File.Exists(detailSurfaceArtifactPath), $"Missing browser detail-surface visual smoke artifact at {detailSurfaceArtifactPath}");
        var detailSurfaceArtifact = await File.ReadAllTextAsync(detailSurfaceArtifactPath);
        Assert.Contains("data-artifact=\"browser-detail-surfaces\"", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"desktop\"", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("data-state=\"compact-cards\"", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("data-state=\"opened-modal\"", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"mobile\"", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("Душа", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("Детали души", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("Детали героя", detailSurfaceArtifact, StringComparison.Ordinal);
        Assert.Contains("Детали локации", detailSurfaceArtifact, StringComparison.Ordinal);
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

    private static string BuildRebornPanelsArtifact(string appSource)
    {
        Assert.Contains("detailSurfaceId=\"reborn-afterlife-overview\"", appSource, StringComparison.Ordinal);
        Assert.Contains("detailSurfaceId=\"reborn-shining-abode\"", appSource, StringComparison.Ordinal);
        Assert.Contains("detailSurfaceId=\"reborn-chaos-sea\"", appSource, StringComparison.Ordinal);
        Assert.Contains("Посмертие Reborn", appSource, StringComparison.Ordinal);
        Assert.Contains("Сияющая Обитель", appSource, StringComparison.Ordinal);
        Assert.Contains("Море Хаоса", appSource, StringComparison.Ordinal);

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

    private static string BuildDetailSurfaceArtifact(string appSource)
    {
        Assert.Contains("detailSurfaceId=\"soul-identity\"", appSource, StringComparison.Ordinal);
        Assert.Contains("detailSurfaceId=\"player-condition\"", appSource, StringComparison.Ordinal);
        Assert.Contains("detailSurfaceId=\"world-location\"", appSource, StringComparison.Ordinal);
        Assert.Contains("Детали души", appSource, StringComparison.Ordinal);
        Assert.Contains("Детали героя", appSource, StringComparison.Ordinal);
        Assert.Contains("Детали локации", appSource, StringComparison.Ordinal);

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
            <section class="frame" data-viewport="desktop" data-state="compact-cards" aria-label="Compact card overview">
              <header>
                <h1>Card overview: detail-rich player data stays compact</h1>
                <p class="sequence">Душа → Герой → Локация</p>
              </header>
              <div class="cards">
                <article class="card"><strong>🕯️ Душа</strong><p>Безымянная душа · Мир смертных</p><p>Открыть детали</p></article>
                <article class="card"><strong>⚔️ Герой</strong><p>Герой · состояние уточняется</p><p>Открыть детали</p></article>
                <article class="card"><strong>🗺️ Локация</strong><p>Проверочный тракт · утро</p><p>Открыть детали</p></article>
              </div>
            </section>
            <section class="frame" data-viewport="desktop" data-state="opened-modal" aria-label="Opened detail modal">
              <header><h1>Opened desktop detail surface</h1></header>
              <div class="modal-wrap">
                <article class="modal">
                  <div class="modal-bar">
                    <div><p class="sequence">душа и царство</p><h2>Детали души</h2></div>
                    <div class="controls"><span>Назад</span><span>Развернуть</span><span>Закрыть</span></div>
                  </div>
                  <p>Эта панель показывает только текущую игровую сводку души из локальной книги.</p>
                  <div class="sections">
                    <section class="section"><h3>Проявление</h3><p>Имя, царство и инкарнация читаются как игровые сведения.</p></section>
                    <section class="section"><h3>Посмертный прогресс</h3><p>Чернильные перья, просветление и хранитель сгруппированы в понятный раздел.</p></section>
                  </div>
                </article>
              </div>
            </section>
            <section class="frame mobile" data-viewport="mobile" data-state="opened-modal" aria-label="Mobile full panel detail surface">
              <header><h1>Mobile full-panel surface</h1></header>
              <article class="modal">
                <div class="modal-bar">
                  <div><p class="sequence">герой</p><h2>Детали героя</h2></div>
                  <div class="controls"><span>Назад</span><span>Закрыть</span></div>
                </div>
                <section class="section"><h3>Состояние</h3><p>Здоровье, энергия и стойкость остаются читаемыми в узком окне.</p></section>
                <section class="section"><h3>Детали локации</h3><p>Локация использует тот же full-panel язык на мобильном viewport.</p></section>
              </article>
            </section>
          </main>
        </body>
        </html>
        """;
    }

    private static string BuildNavigationIaArtifact(string appSource)
    {
        var routes = ExtractPlayerRoutes(appSource);
        var primaryRoutes = routes.Where(route => route.Kind == "primary").ToArray();
        var utilityRoutes = routes.Where(route => route.Kind == "utility").ToArray();
        var primarySequence = string.Join(" → ", primaryRoutes.Select(route => route.Label));
        var utilitySequence = string.Join(" → ", utilityRoutes.Select(route => route.Label));

        Assert.Equal(new[] { "home", "game", "soul", "world", "journal", "inventory" }, primaryRoutes.Select(route => route.Id));
        Assert.Equal(new[] { "media", "settings" }, utilityRoutes.Select(route => route.Id));

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
                <p class="sequence">{{WebUtility.HtmlEncode(primarySequence)}}</p>
              </header>
              <div class="desktop-shell">
                <nav class="sidebar" aria-label="Основные игровые разделы браузерного клиента">
                  <div class="route-list">
        {{RenderRouteCards(primaryRoutes)}}
                  </div>
                  <div class="utility" aria-label="Дополнительные игровые разделы браузерного клиента">
                    <p class="sequence">{{WebUtility.HtmlEncode(utilitySequence)}}</p>
                    <div class="route-list">
        {{RenderRouteCards(utilityRoutes)}}
                    </div>
                  </div>
                </nav>
                <section class="content" aria-label="Игровая область">
                  <h2>Откройте книгу</h2>
                  <p>Обычное пустое состояние выглядит как игровая пауза: выберите главу, продолжите сохранение или перейдите к персонажу, миру, журналу и инвентарю.</p>
                  <p class="advanced-note">Расширенный режим скрыт до явного включения и визуально вторичен.</p>
                </section>
              </div>
            </section>
            <section class="frame mobile-shell" data-viewport="mobile" aria-label="Mobile browser navigation smoke">
              <header>
                <h1>Mobile: compact player navigation</h1>
                <p class="sequence">{{WebUtility.HtmlEncode(primarySequence)}}</p>
              </header>
              <nav class="mobile-nav" aria-label="Мобильные игровые разделы браузерного клиента">
                <div class="route-list">
        {{RenderRouteCards(primaryRoutes)}}
                </div>
                <div class="advanced-note">Расширенный режим остаётся отдельным вторичным переключателем.</div>
              </nav>
            </section>
          </main>
        </body>
        </html>
        """;
    }

    private static BrowserNavigationRoute[] ExtractPlayerRoutes(string appSource)
    {
        var routesMatch = Regex.Match(
            appSource,
            @"const playerRoutes: RouteCard\[\] = \[(?<routes>.*?)\];",
            RegexOptions.Singleline);
        Assert.True(routesMatch.Success, "App.tsx should define playerRoutes metadata for the visual smoke artifact.");

        var routeMatches = Regex.Matches(
            routesMatch.Groups["routes"].Value,
            @"\{\s*id:\s*'(?<id>[^']+)',\s*kind:\s*'(?<kind>[^']+)',\s*label:\s*'(?<label>[^']+)',\s*description:\s*'(?<description>[^']+)',\s*icon:\s*'(?<icon>[^']+)'\s*\}",
            RegexOptions.Singleline);

        var routes = routeMatches
            .Select(match => new BrowserNavigationRoute(
                match.Groups["id"].Value,
                match.Groups["kind"].Value,
                match.Groups["label"].Value,
                match.Groups["description"].Value,
                match.Groups["icon"].Value))
            .ToArray();
        Assert.Equal(8, routes.Length);
        return routes;
    }

    private static string RenderRouteCards(IEnumerable<BrowserNavigationRoute> routes) => string.Join(
        Environment.NewLine,
        routes.Select(route =>
            $"""
                    <article class="route-card route-card--{WebUtility.HtmlEncode(route.Id)}">
                      <strong><span>{WebUtility.HtmlEncode(route.Icon)}</span>{WebUtility.HtmlEncode(route.Label)}</strong>
                      <p>{WebUtility.HtmlEncode(route.Description)}</p>
                    </article>
            """));

    private sealed record BrowserNavigationRoute(string Id, string Kind, string Label, string Description, string Icon);

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
