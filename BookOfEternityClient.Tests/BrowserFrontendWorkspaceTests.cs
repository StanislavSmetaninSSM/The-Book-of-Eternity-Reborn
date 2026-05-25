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

        Assert.Contains("C# API остаётся источником истины", app, StringComparison.Ordinal);
        Assert.DoesNotContain("debug dashboard", app, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var styles = File.ReadAllText(Path.Combine(FrontendRoot, "src", "styles.css"));

        Assert.Contains("playerRoutes", app, StringComparison.Ordinal);
        Assert.Contains("activeRoute", app, StringComparison.Ordinal);
        Assert.Contains("advancedEnabled", app, StringComparison.Ordinal);
        Assert.Contains("loadBrowserState", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.getMainMenu", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.getGameScreen", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.getSessionStatus", app, StringComparison.Ordinal);
        Assert.Contains("browserApi.getLifecycleDashboard", app, StringComparison.Ordinal);
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
        Assert.Contains("Технические подробности доступны после явного включения расширенного режима", app, StringComparison.Ordinal);
        Assert.DoesNotContain("setAdvancedEnabled(true)", app, StringComparison.Ordinal);
        Assert.DoesNotContain("typed BrowserApiClient", app, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint.id", app, StringComparison.Ordinal);
        Assert.Contains(".browser-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".route-grid", styles, StringComparison.Ordinal);
        Assert.Contains(".advanced-diagnostics", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 840px)", styles, StringComparison.Ordinal);
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
}
