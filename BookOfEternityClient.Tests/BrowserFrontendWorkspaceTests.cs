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
        Assert.Equal("npm run typecheck && npm run test:player-facing && npm run build", scripts.GetProperty("verify").GetString());
        Assert.Equal("tsc -p tsconfig.player-facing-tests.json && node ../TestResults/browser-frontend-player-facing-tests/test/playerFacingCommandResult.test.js && node ../TestResults/browser-frontend-player-facing-tests/test/gameLauncherMenuLayout.test.js && vitest run test/playerCopyRobustness.test.ts test/realmTheming.test.ts", scripts.GetProperty("test:player-facing").GetString());

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
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "tsconfig.player-facing-tests.json")));
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
        var app = ReadFrontendSource("App.tsx");

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

        Assert.Contains("<ShellProvider>", app, StringComparison.Ordinal);
        Assert.DoesNotContain("debug dashboard", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source of truth", app, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReactAppShell_RendersCurrentTabCommandShellAndSharedState()
    {
        var app = ReadFrontendSource("App.tsx");
        var shellContext = ReadFrontendSource("context", "ShellContext.tsx");
        var shellStateHook = ReadFrontendSource("hooks", "useShellState.ts");
        var styles = ReadFrontendStyles();

        Assert.Contains("import { ConnectionBanner } from './components/ConnectionBanner';", app, StringComparison.Ordinal);
        Assert.Contains("import { TabBar } from './components/TabBar';", app, StringComparison.Ordinal);
        Assert.Contains("import { SceneView } from './components/SceneView';", app, StringComparison.Ordinal);
        Assert.Contains("import { StatusView } from './components/StatusView';", app, StringComparison.Ordinal);
        Assert.Contains("import { HelpView } from './components/HelpView';", app, StringComparison.Ordinal);
        Assert.Contains("import { SettingsView } from './components/SettingsView';", app, StringComparison.Ordinal);
        Assert.Contains("import { UnifiedInput } from './components/UnifiedInput';", app, StringComparison.Ordinal);
        Assert.Contains("import { GameLauncher } from './components/GameLauncher';", app, StringComparison.Ordinal);
        Assert.Contains("<ConnectionBanner />", app, StringComparison.Ordinal);
        Assert.Contains("{!isLauncherRoute && <TabBar />}", app, StringComparison.Ordinal);
        Assert.Contains("<section className={`content-area${isLauncherRoute ? ' content-area--launcher' : ''}`} aria-live=\"polite\">", app, StringComparison.Ordinal);
        Assert.Contains("<GameLauncher menu={menu} />", app, StringComparison.Ordinal);
        Assert.Contains("{!isLauncherRoute && <UnifiedInput />}", app, StringComparison.Ordinal);
        Assert.Contains("case 'scene': return <SceneView />;", app, StringComparison.Ordinal);
        Assert.Contains("case 'status': return <StatusView />;", app, StringComparison.Ordinal);
        Assert.Contains("case 'help': return <HelpView />;", app, StringComparison.Ordinal);
        Assert.Contains("case 'settings': return <SettingsView />;", app, StringComparison.Ordinal);

        Assert.Contains("export type TabId = 'scene' | 'status' | 'help' | 'settings';", shellContext, StringComparison.Ordinal);
        Assert.Contains("const [activeRoute, setActiveRouteState] = useState<RouteId>('home');", shellContext, StringComparison.Ordinal);
        Assert.Contains("const activeTab = useMemo(() => routeToTab(activeRoute), [activeRoute]);", shellContext, StringComparison.Ordinal);
        Assert.Contains("setActiveRouteState(tabToRoute(tab));", shellContext, StringComparison.Ordinal);
        Assert.Contains("setActiveRouteState('game');", shellContext, StringComparison.Ordinal);
        Assert.Contains("const [advancedEnabled, setAdvancedEnabledState] = useState(false);", shellContext, StringComparison.Ordinal);
        Assert.Contains("const { shellState, loadBrowserState } = useShellState(advancedEnabled);", shellContext, StringComparison.Ordinal);
        Assert.Contains("browserApi.submitPlayerAction({ text: normalized })", shellContext, StringComparison.Ordinal);
        Assert.Contains("browserApi.executeExplorerCommand({ command, advancedEnabled })", shellContext, StringComparison.Ordinal);
        Assert.Contains("composerSubmissionInFlight", shellContext, StringComparison.Ordinal);
        Assert.DoesNotContain("`/prose ${normalized}`", shellContext, StringComparison.Ordinal);
        Assert.DoesNotContain("/prose", shellContext, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("await Promise.allSettled([", shellStateHook, StringComparison.Ordinal);
        Assert.Contains("browserApi.getMainMenu()", shellStateHook, StringComparison.Ordinal);
        Assert.Contains("browserApi.getGameScreen()", shellStateHook, StringComparison.Ordinal);
        Assert.Contains("browserApi.getClientSettings()", shellStateHook, StringComparison.Ordinal);
        Assert.Contains("if (advancedEnabled)", shellStateHook, StringComparison.Ordinal);
        Assert.Contains("browserApi.getLifecycleDashboard()", shellStateHook, StringComparison.Ordinal);
        Assert.Contains("browserApi.getCommandCoverage()", shellStateHook, StringComparison.Ordinal);

        foreach (var staleSnippet in new[] { "./routes/", "<Sidebar />", "ActionPalette", "from './components/Composer'", "playerRoutes", "route-grid--primary", "route-grid--utility" })
        {
            Assert.DoesNotContain(staleSnippet, app, StringComparison.Ordinal);
        }

        Assert.Contains(".browser-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".content-area", styles, StringComparison.Ordinal);
        Assert.Contains(".unified-input", styles, StringComparison.Ordinal);
        Assert.Contains(".tab-bar", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserTabBar_UsesSharedShortcutContractAndBlocksTextInputs()
    {
        var tabBar = ReadFrontendSource("components", "TabBar.tsx");
        var config = ReadFrontendSource("components", "tabBarConfig.ts");
        var styles = ReadFrontendStyles();

        Assert.False(File.Exists(Path.Combine(FrontendRoot, "src", "components", "NavBar.tsx")), "The stale NavBar component should not be compiled.");
        Assert.False(File.Exists(Path.Combine(FrontendRoot, "src", "components", "navBarConfig.ts")), "The stale navBarConfig contract should not be restored.");

        Assert.Contains("export interface TabNavItem", config, StringComparison.Ordinal);
        Assert.Contains("export const tabNav: readonly TabNavItem[]", config, StringComparison.Ordinal);
        Assert.Contains("id: 'scene'", config, StringComparison.Ordinal);
        Assert.Contains("id: 'status'", config, StringComparison.Ordinal);
        Assert.Contains("id: 'help'", config, StringComparison.Ordinal);
        Assert.Contains("id: 'settings'", config, StringComparison.Ordinal);
        Assert.Contains("shortcut: '1'", config, StringComparison.Ordinal);
        Assert.Contains("shortcut: '4'", config, StringComparison.Ordinal);
        Assert.Contains("export function resolveTabShortcut", config, StringComparison.Ordinal);

        Assert.Contains("function isShortcutBlockedTarget", tabBar, StringComparison.Ordinal);
        Assert.Contains("target instanceof HTMLInputElement", tabBar, StringComparison.Ordinal);
        Assert.Contains("target instanceof HTMLTextAreaElement", tabBar, StringComparison.Ordinal);
        Assert.Contains("target instanceof HTMLSelectElement", tabBar, StringComparison.Ordinal);
        Assert.Contains("document.addEventListener('keydown', handleKeyDown);", tabBar, StringComparison.Ordinal);
        Assert.Contains("return () => document.removeEventListener('keydown', handleKeyDown);", tabBar, StringComparison.Ordinal);
        Assert.Contains("<nav className=\"tab-bar\" role=\"tablist\" aria-label=\"Навигация\">", tabBar, StringComparison.Ordinal);
        Assert.Contains("aria-selected={activeTab === tab.id}", tabBar, StringComparison.Ordinal);
        Assert.Contains("setActiveTab(tab.id)", tabBar, StringComparison.Ordinal);
        Assert.Contains("Ход {gameScreen.world.turnNumber}", tabBar, StringComparison.Ordinal);

        Assert.Contains(".tab-bar {", styles, StringComparison.Ordinal);
        Assert.Contains(".tab-bar__tab.is-active", styles, StringComparison.Ordinal);
        Assert.DoesNotContain(".nav-bar", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserCurrentViews_RenderPlayerFacingMinimalShell()
    {
        var sceneView = ReadFrontendSource("components", "SceneView.tsx");
        var statusView = ReadFrontendSource("components", "StatusView.tsx");
        var helpView = ReadFrontendSource("components", "HelpView.tsx");
        var unifiedInput = ReadFrontendSource("components", "UnifiedInput.tsx");
        var styles = ReadFrontendStyles();

        Assert.Contains("import { SceneHero } from './SceneHero';", sceneView, StringComparison.Ordinal);
        Assert.Contains("import { CommandResultView } from './CommandResultView';", sceneView, StringComparison.Ordinal);
        Assert.Contains("if (isCommandView)", sceneView, StringComparison.Ordinal);
        Assert.Contains("return <CommandResultView />;", sceneView, StringComparison.Ordinal);
        Assert.Contains("className=\"scene-quick-actions\"", sceneView, StringComparison.Ordinal);
        Assert.Contains("onClick={() => void onCommand(action.advancedCommand)}", sceneView, StringComparison.Ordinal);
        Assert.DoesNotContain("ActionPalette", sceneView, StringComparison.Ordinal);
        Assert.DoesNotContain("<Composer", sceneView, StringComparison.Ordinal);

        Assert.Contains("<h3>🎭 Персонаж</h3>", statusView, StringComparison.Ordinal);
        Assert.Contains("<h3>🕯️ Душа</h3>", statusView, StringComparison.Ordinal);
        Assert.Contains("<h3>🗺️ Мир</h3>", statusView, StringComparison.Ordinal);
        Assert.Contains("<h3>✨ Посмертие</h3>", statusView, StringComparison.Ordinal);
        Assert.Contains("function StatusBar", statusView, StringComparison.Ordinal);
        Assert.Contains("className=\"status-bar\"", statusView, StringComparison.Ordinal);

        Assert.Contains("GROUP_LABELS", helpView, StringComparison.Ordinal);
        Assert.Contains("Персонаж и душа", helpView, StringComparison.Ordinal);
        Assert.Contains("Мир смертных", helpView, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"🔍 Поиск команды...\"", helpView, StringComparison.Ordinal);
        Assert.Contains("cmd.browserStatus === 'not-browser-executable'", helpView, StringComparison.Ordinal);
        Assert.Contains("void executeCommand(command);", helpView, StringComparison.Ordinal);
        Assert.Contains("setActiveTab('scene');", helpView, StringComparison.Ordinal);

        Assert.Contains("className=\"unified-input\"", unifiedInput, StringComparison.Ordinal);
        Assert.Contains("submitComposerText(e.currentTarget.value);", unifiedInput, StringComparison.Ordinal);
        Assert.Contains("<CommandAutocomplete", unifiedInput, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Опишите действие или введите /команду...\"", unifiedInput, StringComparison.Ordinal);
        Assert.Contains("disabled={!canSubmit}", unifiedInput, StringComparison.Ordinal);

        Assert.Contains(".scene-view", styles, StringComparison.Ordinal);
        Assert.Contains(".scene-quick-actions", styles, StringComparison.Ordinal);
        Assert.Contains(".status-view", styles, StringComparison.Ordinal);
        Assert.Contains(".help-view", styles, StringComparison.Ordinal);
        Assert.Contains(".unified-input", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserSettingsAndDiagnostics_KeepTechnicalSurfacesExplicit()
    {
        var app = ReadFrontendSource("App.tsx");
        var settingsView = ReadFrontendSource("components", "SettingsView.tsx");
        var diagnostics = ReadFrontendSource("components", "AdvancedDiagnostics.tsx");
        var styles = ReadFrontendStyles();

        Assert.Contains("BrowserClientSettingsDto", settingsView, StringComparison.Ordinal);
        Assert.Contains("BrowserClientSettingsUpdateRequest", settingsView, StringComparison.Ordinal);
        Assert.Contains("browserApi.updateClientSettings", settingsView, StringComparison.Ordinal);
        Assert.Contains("updateQueue", settingsView, StringComparison.Ordinal);
        Assert.Contains("setTimeout(() =>", settingsView, StringComparison.Ordinal);
        Assert.Contains("Язык клиента", settingsView, StringComparison.Ordinal);
        Assert.Contains("Сложность", settingsView, StringComparison.Ordinal);
        Assert.Contains("Показывать мысли ГМа", settingsView, StringComparison.Ordinal);
        Assert.Contains("Звук", settingsView, StringComparison.Ordinal);
        Assert.Contains("Доступность", settingsView, StringComparison.Ordinal);
        Assert.Contains("Расширенный режим", settingsView, StringComparison.Ordinal);
        Assert.Contains("Показывать технические данные", settingsView, StringComparison.Ordinal);
        Assert.Contains("setAdvancedEnabled((v) => !v)", settingsView, StringComparison.Ordinal);
        Assert.Contains("--browser-font-scale", app, StringComparison.Ordinal);
        Assert.Contains("--browser-ui-scale", app, StringComparison.Ordinal);
        Assert.Contains("is-reduced-motion", app, StringComparison.Ordinal);
        Assert.Contains("is-contrast-friendly", app, StringComparison.Ordinal);

        Assert.Contains("if (!advancedEnabled || !readyState)", diagnostics, StringComparison.Ordinal);
        Assert.Contains("return null;", diagnostics, StringComparison.Ordinal);
        Assert.Contains("className=\"advanced-diagnostics\"", diagnostics, StringComparison.Ordinal);
        Assert.Contains("CommandCoverageMatrix", diagnostics, StringComparison.Ordinal);
        Assert.Contains("browserApiContractSummary.endpointDocs", diagnostics, StringComparison.Ordinal);
        Assert.Contains("Подробности проверки", diagnostics, StringComparison.Ordinal);

        Assert.DoesNotContain("<AdvancedDiagnosticsPanel />", app, StringComparison.Ordinal);
        Assert.DoesNotContain("browserApiContractSummary.endpointDocs", app, StringComparison.Ordinal);
        Assert.Contains(".settings-view", styles, StringComparison.Ordinal);
        Assert.Contains(".settings-card", styles, StringComparison.Ordinal);
        Assert.Contains(".advanced-diagnostics", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserDesignSystem_HasMaintainableCssStructureAndVisualTokens()
    {
        var entryStyles = File.ReadAllText(Path.Combine(FrontendRoot, "src", "styles.css"));
        var styles = ReadFrontendStyles();
        var app = ReadFrontendSource("App.tsx");
        var apiClient = ReadFrontendSource("api", "client.ts");

        foreach (var fileName in new[] { "tokens.css", "base.css", "components.css", "layout.css", "motion.css", "command-ui.css", "sidebar.css", "hero.css", "realms.css" })
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
        Assert.Contains("@media (max-width: 640px)", styles, StringComparison.Ordinal);
        Assert.Contains(".browser-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".tab-bar", styles, StringComparison.Ordinal);
        Assert.Contains(".content-area", styles, StringComparison.Ordinal);
        Assert.Contains(".unified-input", styles, StringComparison.Ordinal);
        Assert.Contains(".scene-view", styles, StringComparison.Ordinal);
        Assert.Contains(".status-view", styles, StringComparison.Ordinal);
        Assert.Contains(".settings-view", styles, StringComparison.Ordinal);

        Assert.Contains("data-theme-key={realmTheme.key}", app, StringComparison.Ordinal);
        Assert.Contains("formatSessionStatus(", ReadFrontendSource("utils", "formatters.ts"), StringComparison.Ordinal);
        Assert.Contains("formatTurnStateLabel(", ReadFrontendSource("utils", "formatters.ts"), StringComparison.Ordinal);
        Assert.Contains("formatQteStateLabel(", ReadFrontendSource("utils", "formatters.ts"), StringComparison.Ordinal);
        Assert.DoesNotContain("Book of Eternity Reborn · Browser Client", app, StringComparison.Ordinal);
        Assert.DoesNotContain("player-facing", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C# host", apiClient, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("локальный ресурс", apiClient, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("локальный игровой клиент", apiClient, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BrowserLauncherBackgroundArt_HasTrackedLocalProvenanceAndReadabilityGuards()
    {
        var backgroundPath = Path.Combine(FrontendRoot, "public", "main-menu-bg.webp");
        var sourceNotePath = Path.Combine(FrontendRoot, "public", "main-menu-bg.source.md");
        var launcher = ReadFrontendSource("components", "GameLauncher.tsx");
        var styles = ReadFrontendStyles();

        Assert.True(File.Exists(backgroundPath), $"Missing local launcher background art at {backgroundPath}");
        Assert.True(new FileInfo(backgroundPath).Length > 50 * 1024, "Launcher background art should be tracked as a real local bitmap asset.");
        Assert.True(File.Exists(sourceNotePath), $"Missing launcher background source note at {sourceNotePath}");

        var sourceNote = File.ReadAllText(sourceNotePath);
        Assert.Contains("Pollinations AI API", sourceNote, StringComparison.Ordinal);
        Assert.Contains("model=flux", sourceNote, StringComparison.Ordinal);
        Assert.Contains("1920x1080", sourceNote, StringComparison.Ordinal);
        Assert.Contains("dark library with arcane tomes", sourceNote, StringComparison.Ordinal);
        Assert.Contains("cosmic purple/teal mists", sourceNote, StringComparison.Ordinal);
        Assert.Contains("No external runtime dependency", sourceNote, StringComparison.Ordinal);
        Assert.Contains("No text, logos, or third-party IP", sourceNote, StringComparison.Ordinal);

        Assert.Contains("<div className=\"launcher-art-bg\" aria-hidden=\"true\">", launcher, StringComparison.Ordinal);
        Assert.Contains("<img src=\"/main-menu-bg.webp\" alt=\"\" />", launcher, StringComparison.Ordinal);
        Assert.Contains(".launcher-art-bg img", styles, StringComparison.Ordinal);
        Assert.Contains("object-fit: cover;", styles, StringComparison.Ordinal);
        Assert.Contains("object-position: center 30%;", styles, StringComparison.Ordinal);
        Assert.Contains("filter: saturate(0.7) brightness(0.5);", styles, StringComparison.Ordinal);
        Assert.Contains(".launcher-art-bg::after", styles, StringComparison.Ordinal);
        Assert.Contains("linear-gradient(to bottom", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserLegacyRouteAndDashboardContracts_AreNotRevivedBySourceGuards()
    {
        var app = ReadFrontendSource("App.tsx");
        var tabBar = ReadFrontendSource("components", "TabBar.tsx");
        var sceneView = ReadFrontendSource("components", "SceneView.tsx");
        var sourceGuard = File.ReadAllText(Path.Combine(FrontendRoot, "test", "uiStructure.test.ts"));
        var routeExtractionGuard = File.ReadAllText(Path.Combine(FrontendRoot, "test", "appRouteExtraction.test.ts"));

        Assert.DoesNotContain("HomeRoute", app, StringComparison.Ordinal);
        Assert.DoesNotContain("GameRoute", app, StringComparison.Ordinal);
        Assert.DoesNotContain("SoulRoute", app, StringComparison.Ordinal);
        Assert.DoesNotContain("WorldRoute", app, StringComparison.Ordinal);
        Assert.DoesNotContain("ActionPalette", app, StringComparison.Ordinal);
        Assert.DoesNotContain("ActionCard", app, StringComparison.Ordinal);
        Assert.DoesNotContain("NavBar", app, StringComparison.Ordinal);
        Assert.Contains("tabNav.map((tab)", tabBar, StringComparison.Ordinal);
        Assert.Contains("SceneView should not revive the old action palette or composer components", sourceGuard, StringComparison.Ordinal);
        Assert.Contains("should not render the old route/dashboard shell", sourceGuard, StringComparison.Ordinal);
        Assert.Contains("TabBar", routeExtractionGuard, StringComparison.Ordinal);
        Assert.Contains("SceneView", routeExtractionGuard, StringComparison.Ordinal);
        Assert.Contains("StatusView", routeExtractionGuard, StringComparison.Ordinal);
        Assert.Contains("UnifiedInput", routeExtractionGuard, StringComparison.Ordinal);
        Assert.DoesNotContain("../src/routes/", routeExtractionGuard, StringComparison.Ordinal);
        Assert.DoesNotContain("navBarConfig", routeExtractionGuard, StringComparison.Ordinal);
        Assert.DoesNotContain("ActionPalette", sceneView, StringComparison.Ordinal);
    }

    private static string ReadFrontendSource(params string[] relativePath)
    {
        return File.ReadAllText(Path.Combine(new[] { FrontendRoot, "src" }.Concat(relativePath).ToArray()));
    }

    private static string ReadFrontendStyles()
    {
        var paths = new[] { Path.Combine(FrontendRoot, "src", "styles.css") }
            .Concat(Directory.EnumerateFiles(Path.Combine(FrontendRoot, "src", "styles"), "*.css").OrderBy(path => path, StringComparer.Ordinal));

        return string.Join("\n", paths.Select(File.ReadAllText));
    }
}
