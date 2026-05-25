using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BrowserFrontendWorkspaceTests
{
    private static readonly string RepoRoot = TestRepoPaths.RepoRoot;
    private static readonly string FrontendRoot = Path.Combine(RepoRoot, "BookOfEternityClient.WebFrontend");

    [Fact]
    public void FrontendWorkspace_HasViteReactTypeScriptPackageContract()
    {
        var packageJsonPath = Path.Combine(FrontendRoot, "package.json");
        Assert.True(File.Exists(packageJsonPath), $"Missing {packageJsonPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        var root = document.RootElement;

        Assert.Equal("book-of-eternity-reborn-webfrontend", root.GetProperty("name").GetString());
        Assert.True(root.GetProperty("private").GetBoolean());
        Assert.Equal("module", root.GetProperty("type").GetString());

        var scripts = root.GetProperty("scripts");
        Assert.Equal("vite --host 127.0.0.1", scripts.GetProperty("dev").GetString());
        Assert.Equal("tsc --noEmit -p tsconfig.app.json && tsc --noEmit -p tsconfig.node.json", scripts.GetProperty("typecheck").GetString());
        Assert.Equal("npm run typecheck && vite build", scripts.GetProperty("build").GetString());
        Assert.Equal("vite preview --host 127.0.0.1", scripts.GetProperty("preview").GetString());

        var dependencies = root.GetProperty("dependencies");
        Assert.True(dependencies.TryGetProperty("react", out _));
        Assert.True(dependencies.TryGetProperty("react-dom", out _));

        var devDependencies = root.GetProperty("devDependencies");
        Assert.True(devDependencies.TryGetProperty("@vitejs/plugin-react", out _));
        Assert.True(devDependencies.TryGetProperty("vite", out _));
        Assert.True(devDependencies.TryGetProperty("typescript", out _));
        Assert.True(devDependencies.TryGetProperty("@types/react", out _));
        Assert.True(devDependencies.TryGetProperty("@types/react-dom", out _));
    }

    [Fact]
    public void FrontendWorkspace_HasVerifyScriptAndCiFrontendWorkflow()
    {
        var packageJsonPath = Path.Combine(FrontendRoot, "package.json");
        using var document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        var scripts = document.RootElement.GetProperty("scripts");
        Assert.Equal("npm run typecheck && npm run build", scripts.GetProperty("verify").GetString());

        var workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "dotnet-ci.yml"));
        Assert.Contains("Setup Node", workflow, StringComparison.Ordinal);
        Assert.Contains("node-version: 22.x", workflow, StringComparison.Ordinal);
        Assert.Contains("cache-dependency-path: BookOfEternityClient.WebFrontend/package-lock.json", workflow, StringComparison.Ordinal);
        Assert.Contains("npm ci --prefix BookOfEternityClient.WebFrontend", workflow, StringComparison.Ordinal);
        Assert.Contains("npm run verify --prefix BookOfEternityClient.WebFrontend", workflow, StringComparison.Ordinal);
        Assert.Contains("browser-smoke-artifacts", workflow, StringComparison.Ordinal);
        Assert.Contains("TestResults/browser-smoke", workflow, StringComparison.Ordinal);

        var gitignore = File.ReadAllText(Path.Combine(RepoRoot, ".gitignore"));
        Assert.Contains("/TestResults/", gitignore, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendWorkspace_HasStrictViteTypeScriptStructure()
    {
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "index.html")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "vite.config.ts")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "tsconfig.json")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "tsconfig.app.json")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "tsconfig.node.json")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "main.tsx")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "App.tsx")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "styles.css")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "vite-env.d.ts")));

        var appConfig = File.ReadAllText(Path.Combine(FrontendRoot, "tsconfig.app.json"));
        Assert.Contains("\"strict\": true", appConfig, StringComparison.Ordinal);
        Assert.Contains("\"jsx\": \"react-jsx\"", appConfig, StringComparison.Ordinal);

        var viteConfig = File.ReadAllText(Path.Combine(FrontendRoot, "vite.config.ts"));
        Assert.Contains("@vitejs/plugin-react", viteConfig, StringComparison.Ordinal);
        Assert.Contains("outDir: 'dist'", viteConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendWorkspace_IgnoresGeneratedNodeArtifacts()
    {
        var gitignore = File.ReadAllText(Path.Combine(RepoRoot, ".gitignore"));

        Assert.Contains("/BookOfEternityClient.WebFrontend/node_modules/", gitignore, StringComparison.Ordinal);
        Assert.Contains("/BookOfEternityClient.WebFrontend/dist/", gitignore, StringComparison.Ordinal);
        Assert.Contains("/BookOfEternityClient.WebFrontend/.vite/", gitignore, StringComparison.Ordinal);
        Assert.Contains("/BookOfEternityClient.WebFrontend/*.tsbuildinfo", gitignore, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendWorkspace_DocumentsCSharpAuthorityAndHostHandoff()
    {
        var readme = File.ReadAllText(Path.Combine(FrontendRoot, "README.md"));
        var hostDoc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "web-ui", "local-web-host.md"));
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));

        Assert.Contains("npm install", readme, StringComparison.Ordinal);
        Assert.Contains("npm run dev", readme, StringComparison.Ordinal);
        Assert.Contains("npm run typecheck", readme, StringComparison.Ordinal);
        Assert.Contains("npm run build", readme, StringComparison.Ordinal);
        Assert.Contains("C# runtime remains the authority", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("issue #702", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BookOfEternityClient.WebFrontend/dist/", readme, StringComparison.Ordinal);
        Assert.Contains("public/local-web-ui-shell.html", readme, StringComparison.Ordinal);

        Assert.Contains("BookOfEternityClient.WebFrontend", hostDoc, StringComparison.Ordinal);
        Assert.Contains("npm run build", hostDoc, StringComparison.Ordinal);
        Assert.Contains("#702", hostDoc, StringComparison.Ordinal);
        Assert.Contains("standalone frontend assets", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("local-web-ui-shell.html", hostDoc, StringComparison.Ordinal);

        Assert.Contains("Откройте книгу", app, StringComparison.Ordinal);
        Assert.DoesNotContain("debug dashboard", app, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var styles = ReadFrontendStyles();

        Assert.Contains("playerRoutes", app, StringComparison.Ordinal);
        Assert.Contains("activeRoute", app, StringComparison.Ordinal);
        Assert.Contains("advancedEnabled", app, StringComparison.Ordinal);
        Assert.Contains("loadBrowserState", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.getMainMenu", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.getGameScreen", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.getSessionStatus", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.getAudioSettings", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.getLifecycleDashboard", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.getCommandCoverage", app, StringComparison.Ordinal);
        Assert.Contains("advancedEnabled ? await Promise.all([", app, StringComparison.Ordinal);
        Assert.Contains("Главная", app, StringComparison.Ordinal);
        Assert.Contains("Игра", app, StringComparison.Ordinal);
        Assert.Contains("Душа", app, StringComparison.Ordinal);
        Assert.Contains("Мир", app, StringComparison.Ordinal);
        Assert.Contains("Медиа", app, StringComparison.Ordinal);
        Assert.Contains("Настройки", app, StringComparison.Ordinal);
        Assert.Contains("Расширенный режим", app, StringComparison.Ordinal);
        Assert.Contains("AdvancedDiagnosticsPanel", app, StringComparison.Ordinal);
        Assert.Contains("CommandCoverageMatrix", app, StringComparison.Ordinal);
        Assert.Contains("commandCoverage={commandCoverage}", app, StringComparison.Ordinal);
        Assert.Contains("subcommand.canonicalCommand", app, StringComparison.Ordinal);
        Assert.Contains("subcommand.browserStatus", app, StringComparison.Ordinal);
        Assert.Contains("subcommand.aliases.join", app, StringComparison.Ordinal);
        Assert.Contains("subcommand.followUpIssue", app, StringComparison.Ordinal);
        Assert.Contains("ShellPanel", app, StringComparison.Ordinal);
        Assert.Contains("StatusBar", app, StringComparison.Ordinal);
        Assert.Contains("RealmTheme", app, StringComparison.Ordinal);
        Assert.Contains("playerMessage", app, StringComparison.Ordinal);
        Assert.Contains("ActionMenu", app, StringComparison.Ordinal);
        Assert.Contains("Персонаж / Душа", app, StringComparison.Ordinal);
        Assert.Contains("Подготовить форму", app, StringComparison.Ordinal);
        Assert.Contains("mutationWarning", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.executeExplorerCommand({ command: action.advancedCommand", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.submitPromptSession", app, StringComparison.Ordinal);
        Assert.Contains("renderPromptControl", app, StringComparison.Ordinal);
        Assert.Contains("AudioSettingsPanel", app, StringComparison.Ordinal);
        Assert.Contains("Включить музыку в браузере", app, StringComparison.Ordinal);
        Assert.Contains("autoplayGuidance", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.updateAudioSettings", app, StringComparison.Ordinal);
        Assert.Contains("new Audio()", app, StringComparison.Ordinal);
        Assert.Contains("audioSettingsUpdateQueueRef", app, StringComparison.Ordinal);
        Assert.Contains("audioSettingsUpdateQueueRef.current = audioSettingsUpdateQueueRef.current", app, StringComparison.Ordinal);
        Assert.Contains("Аудио управляется постоянной панелью", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<div><dt>Музыка</dt><dd>{options.musicEnabled", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<div><dt>Звук</dt><dd>{options.soundEnabled", app, StringComparison.Ordinal);
        Assert.Contains("{readyState && <AudioSettingsPanel result={readyState.audio} activeRoute={activeRoute} advancedEnabled={advancedEnabled} />}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<AudioSettingsPanel result={state.audio} activeRoute={activeRoute} />", app, StringComparison.Ordinal);
        Assert.DoesNotContain("useEffect(() => {\n    void audioElement", app, StringComparison.Ordinal);
        Assert.Contains("Технические подробности доступны после явного включения расширенного режима", app, StringComparison.Ordinal);
        Assert.DoesNotContain("setAdvancedEnabled(true)", app, StringComparison.Ordinal);
        Assert.DoesNotContain("getCommandCoverage()", app[..app.IndexOf("advancedEnabled ? await Promise.all([", StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.DoesNotContain("typed BrowserApiClient", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint.id", app, StringComparison.Ordinal);
        Assert.DoesNotContain("action.advancedCommand}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("C# каталога команд", app, StringComparison.Ordinal);
        Assert.DoesNotContain("C# протоколом", app, StringComparison.Ordinal);
        Assert.DoesNotContain("C# DTO", app, StringComparison.Ordinal);
        Assert.Contains(".browser-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".route-grid", styles, StringComparison.Ordinal);
        Assert.Contains(".action-menu", styles, StringComparison.Ordinal);
        Assert.Contains(".audio-control-panel", styles, StringComparison.Ordinal);
        Assert.Contains(".advanced-diagnostics", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 840px)", styles, StringComparison.Ordinal);
    }


    [Fact]
    public void BrowserNavigationIa_SeparatesPrimaryPlayerRoutesFromUtilityAndAdvancedRoutes()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var styles = ReadFrontendStyles();
        var readme = File.ReadAllText(Path.Combine(FrontendRoot, "README.md"));
        var hostDoc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "web-ui", "local-web-host.md"));

        Assert.Contains("type RouteKind = 'primary' | 'utility';", app, StringComparison.Ordinal);
        Assert.Contains("const primaryPlayerRoutes = playerRoutes.filter((route) => route.kind === 'primary');", app, StringComparison.Ordinal);
        Assert.Contains("const utilityPlayerRoutes = playerRoutes.filter((route) => route.kind === 'utility');", app, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Основные игровые разделы браузерного клиента\"", app, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Дополнительные игровые разделы браузерного клиента\"", app, StringComparison.Ordinal);
        Assert.Contains("className=\"route-grid route-grid--primary\"", app, StringComparison.Ordinal);
        Assert.Contains("className=\"route-grid route-grid--utility\"", app, StringComparison.Ordinal);

        var routeOrder = new[] { "id: 'home'", "id: 'game'", "id: 'soul'", "id: 'world'", "id: 'journal'", "id: 'inventory'", "id: 'media'", "id: 'settings'" };
        var previousIndex = -1;
        foreach (var marker in routeOrder)
        {
            var index = app.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"Route marker {marker} should appear after the previous player route marker.");
            previousIndex = index;
        }

        Assert.Contains("label: 'Журнал'", app, StringComparison.Ordinal);
        Assert.Contains("label: 'Инвентарь'", app, StringComparison.Ordinal);
        Assert.Contains("function JournalRoute", app, StringComparison.Ordinal);
        Assert.Contains("function InventoryRoute", app, StringComparison.Ordinal);
        Assert.Contains("filterActionSections(game.actionMenu, journalSectionMatchers)", app, StringComparison.Ordinal);
        Assert.Contains("filterActionSections(game.actionMenu, inventorySectionMatchers)", app, StringComparison.Ordinal);
        Assert.Contains("Журнал ждёт главу", app, StringComparison.Ordinal);
        Assert.Contains("Инвентарь ждёт главу", app, StringComparison.Ordinal);
        Assert.Contains("Сводка / Игра / Душа / Мир / Журнал / Инвентарь", app, StringComparison.Ordinal);

        var routeArrayStart = app.IndexOf("const playerRoutes", StringComparison.Ordinal);
        var routeArrayEnd = app.IndexOf("const fallbackTheme", StringComparison.Ordinal);
        Assert.True(routeArrayStart >= 0 && routeArrayEnd > routeArrayStart, "Route metadata should stay near the top of App.tsx.");
        var routeMetadata = app[routeArrayStart..routeArrayEnd];
        Assert.DoesNotContain("Debug", routeMetadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", routeMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("Network", routeMetadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command coverage", routeMetadata, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(".route-grid--primary", styles, StringComparison.Ordinal);
        Assert.Contains(".route-grid--utility", styles, StringComparison.Ordinal);
        Assert.Contains(".route-card--journal", styles, StringComparison.Ordinal);
        Assert.Contains(".route-card--inventory", styles, StringComparison.Ordinal);

        Assert.Contains("#727", readme, StringComparison.Ordinal);
        Assert.Contains("Главная → Игра → Душа → Мир → Журнал → Инвентарь", readme, StringComparison.Ordinal);
        Assert.Contains("#727", hostDoc, StringComparison.Ordinal);
        Assert.Contains("Главная → Игра → Душа → Мир → Журнал → Инвентарь", hostDoc, StringComparison.Ordinal);
    }


    [Fact]
    public void BrowserNavigationIa_InventoryRouteMatchesCurrentActionMetadata()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var fixturePath = Path.Combine(FrontendRoot, "src", "api", "contract-fixtures", "game-screen.json");
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var sections = document.RootElement.GetProperty("actionMenu").GetProperty("sections").EnumerateArray();
        var actionLocations = sections
            .SelectMany(section => section.GetProperty("actions").EnumerateArray().Select(action => new
            {
                SectionId = section.GetProperty("id").GetString(),
                ActionId = action.GetProperty("id").GetString(),
            }))
            .ToArray();

        Assert.Contains(actionLocations, action => action.SectionId == "soul" && action.ActionId == "inventory");
        Assert.Contains(actionLocations, action => action.SectionId == "world" && action.ActionId == "storage_access");
        Assert.Contains(actionLocations, action => action.SectionId == "world" && action.ActionId == "craft");

        Assert.Contains("function matchesActionSectionOrAction", app, StringComparison.Ordinal);
        Assert.Contains("const matchingActions = section.actions.filter((action) => matchesActionSectionOrAction(section, action, matchers));", app, StringComparison.Ordinal);
        Assert.Contains("actions: matchingActions", app, StringComparison.Ordinal);
        Assert.DoesNotContain("return matchers.some((matcher) => haystack.includes", app, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserDesignSystem_HasMaintainableCssStructureAndVisualTokens()
    {
        var entryStyles = File.ReadAllText(Path.Combine(FrontendRoot, "src", "styles.css"));
        var styles = ReadFrontendStyles();
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var apiClient = File.ReadAllText(Path.Combine(FrontendRoot, "src", "api", "client.ts"));
        var readme = File.ReadAllText(Path.Combine(FrontendRoot, "README.md"));
        var hostDoc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "web-ui", "local-web-host.md"));

        foreach (var fileName in new[] { "tokens.css", "base.css", "components.css", "layout.css", "motion.css" })
        {
            Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "styles", fileName)), $"Missing frontend design-system CSS file {fileName}");
            Assert.Contains($"./styles/{fileName}", entryStyles, StringComparison.Ordinal);
        }

        Assert.Contains("--color-ink", styles, StringComparison.Ordinal);
        Assert.Contains("--color-parchment", styles, StringComparison.Ordinal);
        Assert.Contains("--realm-chaos", styles, StringComparison.Ordinal);
        Assert.Contains("--realm-shining", styles, StringComparison.Ordinal);
        Assert.Contains("--state-repair", styles, StringComparison.Ordinal);
        Assert.Contains("--state-qte", styles, StringComparison.Ordinal);
        Assert.Contains("--motion-panel", styles, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", styles, StringComparison.Ordinal);
        Assert.Contains(".design-system-grid", styles, StringComparison.Ordinal);
        Assert.Contains(".route-card--game", styles, StringComparison.Ordinal);
        Assert.Contains(".narrative-card.is-featured", styles, StringComparison.Ordinal);
        Assert.Contains(".shell-panel[data-panel='turn']", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 640px)", styles, StringComparison.Ordinal);

        Assert.Contains("Книга Вечности: Перерождение", app, StringComparison.Ordinal);
        Assert.Contains("data-theme-key={realmTheme.key}", app, StringComparison.Ordinal);
        Assert.Contains("route-card--${route.id}", app, StringComparison.Ordinal);
        Assert.Contains("variant=\"turn\"", app, StringComparison.Ordinal);
        Assert.Contains("formatSessionStatus(", app, StringComparison.Ordinal);
        Assert.Contains("formatTurnStateLabel(", app, StringComparison.Ordinal);
        Assert.Contains("formatQteStateLabel(", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Book of Eternity Reborn · Browser Client", app, StringComparison.Ordinal);
        Assert.DoesNotContain("player-facing", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Текущий realm", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{session.status}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("game_session найден", app, StringComparison.Ordinal);
        Assert.DoesNotContain("eyebrow={game.turnState.state}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("game.qte.notification ?? game.qte.state", app, StringComparison.Ordinal);
        Assert.DoesNotContain("qte.notification ?? qte.error ?? qte.state", app, StringComparison.Ordinal);
        Assert.DoesNotContain("локальный host", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C# host", apiClient, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("локальный ресурс", apiClient, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("QTE и уведомления", app, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("локальный игровой клиент", apiClient, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("toPlayerFacingText(", app, StringComparison.Ordinal);
        Assert.Contains("formatRealmName(soul.realm)", app, StringComparison.Ordinal);
        Assert.Contains("formatDialogueCategory(option.category)", app, StringComparison.Ordinal);
        Assert.Contains("formatTurnStateTitle(", app, StringComparison.Ordinal);
        Assert.Contains("formatTurnStateMessage(", app, StringComparison.Ordinal);
        Assert.Contains("getComposerGuidance(", app, StringComparison.Ordinal);
        Assert.DoesNotContain("gameScreen?.turnState.title", app, StringComparison.Ordinal);
        Assert.DoesNotContain("{game.turnState.title}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("{game.turnState.message}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("placeholder={game.actionComposer.placeholder", app, StringComparison.Ordinal);
        Assert.DoesNotContain("{game.actionComposer.guidance}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("{game.actionComposer.disabledReason}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("{option.category}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("return qte.notification;", app, StringComparison.Ordinal);
        Assert.DoesNotContain("{soul.realm}", app, StringComparison.Ordinal);
        Assert.Contains("[/Slash-команды/gi, 'служебные команды']", app, StringComparison.Ordinal);
        Assert.Contains("[/repair pending turn/gi, 'починка ожидающего хода']", app, StringComparison.Ordinal);
        Assert.Contains("[/\\brepair\\b/gi, 'починка']", app, StringComparison.Ordinal);
        Assert.Contains("toPlayerFacingText(notification.title", app, StringComparison.Ordinal);
        Assert.Contains("toPlayerFacingText(block.text", app, StringComparison.Ordinal);
        Assert.Contains("toPlayerFacingText(block.title", app, StringComparison.Ordinal);
        Assert.Contains("toPlayerFacingText(item.key", app, StringComparison.Ordinal);
        Assert.Contains("toPlayerFacingText(prompt.prompt", app, StringComparison.Ordinal);
        Assert.DoesNotContain("{notification.title}</strong> — {notification.message}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("return <p>{block.text}</p>", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<h5>{block.title}</h5>", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>{item.key}</dt><dd>{item.value}</dd>", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<span>{prompt.prompt}</span>", app, StringComparison.Ordinal);
        Assert.DoesNotContain("placeholder={prompt.placeholder}", app, StringComparison.Ordinal);
        Assert.Contains("toPlayerFacingText(menu.session.continueReason", app, StringComparison.Ordinal);
        Assert.Contains("toPlayerFacingText(action.description", app, StringComparison.Ordinal);
        Assert.Contains("toPlayerFacingText(options.guidance", app, StringComparison.Ordinal);
        Assert.Contains("toPlayerFacingText(playlist.usage", app, StringComparison.Ordinal);
        Assert.Contains("toPlayerFacingText(cue.label", app, StringComparison.Ordinal);
        Assert.DoesNotContain("{menu.session.continueReason}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("action.enabled ? action.description : action.disabledReason", app, StringComparison.Ordinal);
        Assert.DoesNotContain("{options.guidance}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("{result.playerMessage}</p>", app, StringComparison.Ordinal);
        Assert.DoesNotContain("${playlist.label}: ${playlist.usage}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("{cue.label}: {cue.available ? 'готово' : 'нет файла'}", app, StringComparison.Ordinal);
        Assert.Contains("[/game_session/gi, 'сохранение игры']", app, StringComparison.Ordinal);
        Assert.Contains("[/write-flow/gi, 'запись хода']", app, StringComparison.Ordinal);
        Assert.Contains("[/manual_saves/gi, 'ручные сохранения']", app, StringComparison.Ordinal);
        Assert.Contains("[/autosaves/gi, 'автосохранения']", app, StringComparison.Ordinal);
        Assert.Contains("[/--web/g, 'браузерный режим']", app, StringComparison.Ordinal);
        Assert.Contains("[/snapshot artifact/gi, 'снимок состояния']", app, StringComparison.Ordinal);
        Assert.Contains("[/state\\/contract/gi, 'файлы состояния и контракта']", app, StringComparison.Ordinal);
        Assert.Contains("[/\\boffer\\b/gi, 'предложение']", app, StringComparison.Ordinal);
        Assert.Contains("[/Browser Client/gi, 'браузерный клиент']", app, StringComparison.Ordinal);
        Assert.Contains("[/sound-notification/gi, 'звуковая подсказка']", app, StringComparison.Ordinal);
        Assert.Contains("[/\\brealm\\b/gi, 'царство']", app, StringComparison.Ordinal);
        Assert.Contains("[/repair\\/validation/gi, 'починка и проверка']", app, StringComparison.Ordinal);
        Assert.Contains("[/UI-блокировка/gi, 'блокировка интерфейса']", app, StringComparison.Ordinal);
        Assert.Contains("[/\\bvalidation\\b/gi, 'проверка']", app, StringComparison.Ordinal);
        Assert.Contains("[/game_state\\/meta\\/soul_state\\.json/gi, 'файл души']", app, StringComparison.Ordinal);
        Assert.Contains("[/локальный запись хода/gi, 'локальную запись хода']", app, StringComparison.Ordinal);
        Assert.Contains("[/тот же локальную/gi, 'ту же локальную']", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Валидация:", app, StringComparison.Ordinal);
        Assert.Contains("prompt.allowCustom", app, StringComparison.Ordinal);
        Assert.Contains("Или впишите свой вариант", app, StringComparison.Ordinal);
        Assert.Contains("return prompt.defaultValue;", app, StringComparison.Ordinal);
        Assert.DoesNotContain("return toPlayerFacingText(prompt.defaultValue", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Подробность: ${message}", app, StringComparison.Ordinal);

        Assert.Contains("#685", readme, StringComparison.Ordinal);
        Assert.Contains("src/styles/tokens.css", readme, StringComparison.Ordinal);
        Assert.Contains("dark-fantasy", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#685", hostDoc, StringComparison.Ordinal);
        Assert.Contains("design-system", hostDoc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BrowserDefaultScreen_UsesPlayerFacingCopyAndNeutralEmptyStates()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var styles = ReadFrontendStyles();

        Assert.Contains("Книга Вечности: Перерождение", app, StringComparison.Ordinal);
        Assert.Contains("Откройте книгу", app, StringComparison.Ordinal);
        Assert.Contains("function EmptyState", app, StringComparison.Ordinal);
        Assert.Contains("function EmptyOrFailure", app, StringComparison.Ordinal);
        Assert.Contains("result.kind === 'no-active-session'", app, StringComparison.Ordinal);
        Assert.Contains("return <ApiFailure title={errorTitle}", app, StringComparison.Ordinal);
        Assert.Contains("className=\"empty-state\"", app, StringComparison.Ordinal);
        Assert.Contains(".empty-state", styles, StringComparison.Ordinal);
        Assert.Contains("Технические подробности доступны после явного включения расширенного режима", app, StringComparison.Ordinal);

        Assert.DoesNotContain("<h1 id=\"browser-client-title\">Локальный игровой клиент</h1>", app, StringComparison.Ordinal);
        Assert.DoesNotContain("источник истины", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("маршруты", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("состояние интерфейса", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("посмертные контракты", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("отдельный слой", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Главное меню недоступно", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Игровой экран недоступен", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Данные души недоступны", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Мир недоступен", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Медиа недоступны", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Настройки недоступны", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Сессия недоступна", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Аудио-настройки недоступны", app, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserHomeRoute_RendersPlayerFacingLauncherWithPrimaryCta()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var styles = ReadFrontendStyles();

        Assert.Contains("function GameLauncher", app, StringComparison.Ordinal);
        Assert.Contains("interface LauncherPrimaryAction", app, StringComparison.Ordinal);
        Assert.Contains("selectPrimaryLauncherAction(", app, StringComparison.Ordinal);
        Assert.Contains("launcher-primary-action", app, StringComparison.Ordinal);
        Assert.Contains("launcher-mode-tabs", app, StringComparison.Ordinal);
        Assert.Contains("launcher-save-list", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.loadSave({ saveId: slot.saveId })", app, StringComparison.Ordinal);
        Assert.Contains("onActiveRouteChange('game')", app, StringComparison.Ordinal);
        Assert.Contains("Открыть книгу", app, StringComparison.Ordinal);
        Assert.Contains("Продолжить главу", app, StringComparison.Ordinal);
        Assert.Contains("Начать новую главу", app, StringComparison.Ordinal);
        Assert.Contains("Загрузить сохранение", app, StringComparison.Ordinal);
        Assert.Contains("Настроить клиент", app, StringComparison.Ordinal);
        Assert.Contains("Сведения о книге", app, StringComparison.Ordinal);
        Assert.Contains("className=\"launcher-secondary-actions\"", app, StringComparison.Ordinal);
        Assert.Contains("className=\"advanced-toggle\"", app, StringComparison.Ordinal);
        Assert.Contains("function playerLauncherAboutText", app, StringComparison.Ordinal);
        Assert.Contains("[/debug shell/gi, 'служебная оболочка']", app, StringComparison.Ordinal);
        Assert.Contains("function toLauncherSaveFailureNotice", app, StringComparison.Ordinal);
        Assert.DoesNotContain("setLauncherNotice(toPlayerFacingText(result.data.error", app, StringComparison.Ordinal);
        Assert.Contains("isLauncherMountedRef", app, StringComparison.Ordinal);
        Assert.Contains("isLauncherMountedRef.current = false", app, StringComparison.Ordinal);

        var advancedDiagnosticsIndex = app.IndexOf("function AdvancedDiagnosticsPanel", StringComparison.Ordinal);
        Assert.True(advancedDiagnosticsIndex > 0, "Advanced diagnostics must stay in a separate source section.");
        var playerDefaultAppSlice = app[..advancedDiagnosticsIndex];
        var hasRawDebugShellInPlayerDefaultSlice = playerDefaultAppSlice.Contains("debug shell", StringComparison.OrdinalIgnoreCase);
        var hasExplicitDebugShellReplacement = app.Contains("[/debug shell/gi, 'служебная оболочка']", StringComparison.Ordinal);
        Assert.True(!hasRawDebugShellInPlayerDefaultSlice || hasExplicitDebugShellReplacement, "Player-default launcher copy must not expose raw debug shell wording.");

        Assert.Contains(".game-launcher", styles, StringComparison.Ordinal);
        Assert.Contains(".launcher-primary-action", styles, StringComparison.Ordinal);
        Assert.Contains(".launcher-secondary-actions", styles, StringComparison.Ordinal);
        Assert.Contains(".launcher-mode-tabs", styles, StringComparison.Ordinal);
        Assert.Contains(".launcher-save-list", styles, StringComparison.Ordinal);

        var primaryIndex = app.IndexOf("launcher-primary-action", StringComparison.Ordinal);
        var secondaryIndex = app.IndexOf("launcher-secondary-actions", StringComparison.Ordinal);
        var advancedIndex = app.IndexOf("className=\"advanced-toggle\"", StringComparison.Ordinal);
        Assert.True(primaryIndex > 0, "Launcher primary CTA must be explicit.");
        Assert.True(secondaryIndex > primaryIndex, "Secondary actions must follow the primary CTA.");
        Assert.True(advancedIndex > secondaryIndex, "Advanced mode must stay lower priority than launcher actions in source order.");
    }

    [Fact]
    public void BrowserSidebar_RendersPlayerFacingStatusInsteadOfDebugDashboard()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var styles = ReadFrontendStyles();

        Assert.Contains("function PlayerStatusSidebar", app, StringComparison.Ordinal);
        Assert.Contains("function StatusSummaryCard", app, StringComparison.Ordinal);
        Assert.Contains("className=\"player-status-sidebar\"", app, StringComparison.Ordinal);
        Assert.Contains("Сводка книги", app, StringComparison.Ordinal);
        Assert.Contains("Слой книги", app, StringComparison.Ordinal);
        Assert.Contains("Герой и душа", app, StringComparison.Ordinal);
        Assert.Contains("Сохранение", app, StringComparison.Ordinal);
        Assert.Contains("Ожидание ГМа", app, StringComparison.Ordinal);
        Assert.Contains("Служебная панель", app, StringComparison.Ordinal);
        Assert.Contains("Подробности ремонта, проверки и команд скрыты до явного включения.", app, StringComparison.Ordinal);
        Assert.Contains("formatSidebarSessionSummary(", app, StringComparison.Ordinal);
        Assert.Contains("formatSidebarAudioSummary(", app, StringComparison.Ordinal);
        Assert.Contains("getSidebarFailure(", app, StringComparison.Ordinal);
        Assert.Contains("formatSidebarStatusMetric(", app, StringComparison.Ordinal);
        Assert.Contains("sidebarMenuFailure", app, StringComparison.Ordinal);
        Assert.Contains("sidebarSessionFailure", app, StringComparison.Ordinal);
        Assert.Contains("sidebarGameFailure", app, StringComparison.Ordinal);
        Assert.Contains("attention={Boolean(sidebarGameFailure)}", app, StringComparison.Ordinal);
        Assert.Contains("className=\"warning-text\">{sidebarGameFailure}", app, StringComparison.Ordinal);

        Assert.DoesNotContain("<ShellPanel title=\"Сессия\" eyebrow=\"локальная книга\">", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<ShellPanel title=\"Ход и ремонт\" eyebrow=\"безопасность хода\">", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Проверка: {toPlayerFacingText(gameScreen.turnState.validationLabel", app, StringComparison.Ordinal);
        Assert.DoesNotContain("healthPercentage}%", app, StringComparison.Ordinal);
        Assert.DoesNotContain("energyPercentage}%", app, StringComparison.Ordinal);
        Assert.DoesNotContain("poisePercentage}%", app, StringComparison.Ordinal);

        var sidebarIndex = app.IndexOf("className=\"player-status-sidebar\"", StringComparison.Ordinal);
        var advancedEntryIndex = app.IndexOf("className=\"advanced-sidebar-entry\"", StringComparison.Ordinal);
        var diagnosticsIndex = app.IndexOf("function AdvancedDiagnosticsPanel", StringComparison.Ordinal);
        Assert.True(sidebarIndex > 0, "Player status sidebar must render before advanced entry.");
        Assert.True(advancedEntryIndex > sidebarIndex, "Advanced entry should be lower priority than player status cards.");
        Assert.True(diagnosticsIndex > advancedEntryIndex, "Advanced diagnostics implementation should stay outside the default sidebar source slice.");

        Assert.Contains(".player-status-sidebar", styles, StringComparison.Ordinal);
        Assert.Contains(".status-summary-card", styles, StringComparison.Ordinal);
        Assert.Contains(".advanced-sidebar-entry", styles, StringComparison.Ordinal);
        Assert.Contains(".status-summary-grid", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void ReactAppShell_DocumentsIssue704RoutingAndPlayerAdvancedBoundary()
    {
        var readme = File.ReadAllText(Path.Combine(FrontendRoot, "README.md"));
        var hostDoc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "web-ui", "local-web-host.md"));

        Assert.Contains("#704", readme, StringComparison.Ordinal);
        Assert.Contains("React app shell", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("player-facing routes", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("advanced", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dist/index.html", readme, StringComparison.Ordinal);
        Assert.Contains("#704", hostDoc, StringComparison.Ordinal);
        Assert.Contains("React app shell", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dist/index.html", hostDoc, StringComparison.Ordinal);
        Assert.Contains("Расширенный режим", hostDoc, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendHostContract_UsesExternalAssetsInsteadOfInlineShellBlob()
    {
        var hostSource = File.ReadAllText(Path.Combine(RepoRoot, "BookOfEternityClient", "WebUi", "LocalWebUiHost.cs"));
        var fallbackShell = Path.Combine(FrontendRoot, "public", "local-web-ui-shell.html");

        Assert.True(File.Exists(fallbackShell), $"Missing {fallbackShell}");
        Assert.DoesNotContain("BuildShellHtml", hostSource, StringComparison.Ordinal);
        Assert.DoesNotContain("data-menu-action=\"continue\"", hostSource, StringComparison.Ordinal);
        Assert.DoesNotContain("<!doctype html>", hostSource, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadFrontendStyles()
    {
        var paths = new[]
        {
            Path.Combine(FrontendRoot, "src", "styles.css"),
            Path.Combine(FrontendRoot, "src", "styles", "tokens.css"),
            Path.Combine(FrontendRoot, "src", "styles", "base.css"),
            Path.Combine(FrontendRoot, "src", "styles", "components.css"),
            Path.Combine(FrontendRoot, "src", "styles", "layout.css"),
            Path.Combine(FrontendRoot, "src", "styles", "motion.css"),
        };

        return string.Join("\n", paths.Where(File.Exists).Select(File.ReadAllText));
    }
}
