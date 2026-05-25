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

        Assert.Contains("Локальный клиент остаётся источником истины", app, StringComparison.Ordinal);
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
        Assert.Contains("advancedEnabled ? await browserApi.getLifecycleDashboard()", app, StringComparison.Ordinal);
        Assert.Contains("Главная", app, StringComparison.Ordinal);
        Assert.Contains("Игра", app, StringComparison.Ordinal);
        Assert.Contains("Душа", app, StringComparison.Ordinal);
        Assert.Contains("Мир", app, StringComparison.Ordinal);
        Assert.Contains("Медиа", app, StringComparison.Ordinal);
        Assert.Contains("Настройки", app, StringComparison.Ordinal);
        Assert.Contains("Расширенный режим", app, StringComparison.Ordinal);
        Assert.Contains("AdvancedDiagnosticsPanel", app, StringComparison.Ordinal);
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
        Assert.Contains("{readyState && <AudioSettingsPanel result={readyState.audio} activeRoute={activeRoute} />}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<AudioSettingsPanel result={state.audio} activeRoute={activeRoute} />", app, StringComparison.Ordinal);
        Assert.DoesNotContain("useEffect(() => {\n    void audioElement", app, StringComparison.Ordinal);
        Assert.Contains("Технические подробности доступны после явного включения расширенного режима", app, StringComparison.Ordinal);
        Assert.DoesNotContain("setAdvancedEnabled(true)", app, StringComparison.Ordinal);
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
    public void BrowserDesignSystem_HasMaintainableCssStructureAndVisualTokens()
    {
        var entryStyles = File.ReadAllText(Path.Combine(FrontendRoot, "src", "styles.css"));
        var styles = ReadFrontendStyles();
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
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
        Assert.DoesNotContain("Book of Eternity Reborn · Browser Client", app, StringComparison.Ordinal);
        Assert.DoesNotContain("player-facing", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Текущий realm", app, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("#685", readme, StringComparison.Ordinal);
        Assert.Contains("src/styles/tokens.css", readme, StringComparison.Ordinal);
        Assert.Contains("dark-fantasy", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#685", hostDoc, StringComparison.Ordinal);
        Assert.Contains("design-system", hostDoc, StringComparison.OrdinalIgnoreCase);
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
