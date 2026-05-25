using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LocalWebUiDocumentationTests
{
    [Fact]
    public void LocalWebHostDocs_CoverLaunchSecuritySessionLockAndMigrationLimits()
    {
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "local-web-host.md"));

        Assert.Contains("dotnet run --project BookOfEternityClient", text, StringComparison.Ordinal);
        Assert.Contains("--web", text, StringComparison.Ordinal);
        Assert.Contains("--web-url", text, StringComparison.Ordinal);
        Assert.Contains("Console Mode", text, StringComparison.Ordinal);
        Assert.Contains("Browser Mode", text, StringComparison.Ordinal);
        Assert.Contains("same `game_session` data", text, StringComparison.Ordinal);
        Assert.Contains("<base path>/game_session/", text, StringComparison.Ordinal);
        Assert.Contains("loopback", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0.0.0.0", text, StringComparison.Ordinal);
        Assert.Contains("game_state/control/local_ui_session_lock.json", text, StringComparison.Ordinal);
        Assert.Contains("Stale lock recovery", text, StringComparison.Ordinal);
        Assert.Contains("Pending", text, StringComparison.Ordinal);
        Assert.Contains("Temporary Browser Limitations", text, StringComparison.Ordinal);
        Assert.Contains("console mode remains the complete path", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/shining_treasury", text, StringComparison.Ordinal);
        Assert.Contains("/spiritual_arts", text, StringComparison.Ordinal);
        Assert.Contains("/api/lifecycle/dashboard", text, StringComparison.Ordinal);
        Assert.Contains("/api/lifecycle/validate", text, StringComparison.Ordinal);
        Assert.Contains("/api/explorer/command-coverage", text, StringComparison.Ordinal);
        Assert.Contains("Панель состояния", text, StringComparison.Ordinal);
        Assert.Contains("Troubleshooting", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalWebHostDocs_UseCurrentParityTasksAndCategories()
    {
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "local-web-host.md"));

        Assert.Contains("read-only parity", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("interactive form pending", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status-only", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#590", text, StringComparison.Ordinal);
        Assert.Contains("#591", text, StringComparison.Ordinal);
        Assert.DoesNotContain("#575", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserParityChecklist_CoversFullShellFlows()
    {
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "browser-parity-checklist.md"));

        Assert.Contains("Мир смертных", text, StringComparison.Ordinal);
        Assert.Contains("Море Хаоса", text, StringComparison.Ordinal);
        Assert.Contains("Сияющая Обитель", text, StringComparison.Ordinal);
        Assert.Contains("Духовный бой", text, StringComparison.Ordinal);
        Assert.Contains("История и архив", text, StringComparison.Ordinal);
        Assert.Contains("Диагностика", text, StringComparison.Ordinal);
        Assert.Contains("командная палитра", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("мобиль", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("raw JSON", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("QTE", text, StringComparison.Ordinal);
        Assert.Contains("SVG", text, StringComparison.Ordinal);
        Assert.Contains("z-level", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("layer filter", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalWebHostDocs_SeparatePlayerDefaultFromAdvancedDiagnostics()
    {
        var hostDoc = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "local-web-host.md"));
        var checklist = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "browser-parity-checklist.md"));

        Assert.Contains("root is the #704 React app shell", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Advanced / developer panel", hostDoc, StringComparison.Ordinal);
        Assert.Contains("raw command console", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`/api/*` details", hostDoc, StringComparison.Ordinal);
        Assert.Contains("separate explicit `Расширенный режим` opt-in", hostDoc, StringComparison.Ordinal);
        Assert.Contains("Advanced / developer panel", checklist, StringComparison.Ordinal);
        Assert.Contains("player-facing default", checklist, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public void LocalWebHostDocs_DocumentBrowserSmokeAndGameScreenState()
    {
        var hostDoc = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "local-web-host.md"));
        var checklist = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "browser-parity-checklist.md"));

        Assert.Contains("GET /api/game-screen", hostDoc, StringComparison.Ordinal);
        Assert.Contains("Category=BrowserWebUiSmoke|Category=BrowserWebUiParity", hostDoc, StringComparison.Ordinal);
        Assert.Contains("game-screen state", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("player-facing game screen", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("primary prose action composer", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("read-only game-screen", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BrowserWebUiSmoke", checklist, StringComparison.Ordinal);
        Assert.Contains("BrowserWebUiParity", checklist, StringComparison.Ordinal);
        Assert.Contains("primary prose action composer", checklist, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalWebHostDocs_DocumentFrontendAssetServingContract()
    {
        var hostDoc = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "local-web-host.md"));
        var readme = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "README.md"));

        Assert.Contains("BookOfEternityClient.WebFrontend/dist/", hostDoc, StringComparison.Ordinal);
        Assert.Contains("public/local-web-ui-shell.html", hostDoc, StringComparison.Ordinal);
        Assert.Contains("wwwroot/browser", hostDoc, StringComparison.Ordinal);
        Assert.Contains("C# owns the loopback APIs/runtime", hostDoc, StringComparison.Ordinal);
        Assert.Contains("dist/` is generated and remains git-ignored", hostDoc, StringComparison.Ordinal);
        Assert.Contains("local-web-ui-shell.html", readme, StringComparison.Ordinal);
        Assert.Contains("Generated `dist/`", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalWebHostDocs_DocumentFrontendVerificationPipeline()
    {
        var hostDoc = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "local-web-host.md"));
        var readme = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "README.md"));

        Assert.Contains("#705", hostDoc, StringComparison.Ordinal);
        Assert.Contains("npm ci --prefix BookOfEternityClient.WebFrontend", hostDoc, StringComparison.Ordinal);
        Assert.Contains("npm run verify --prefix BookOfEternityClient.WebFrontend", hostDoc, StringComparison.Ordinal);
        Assert.Contains("Category=BrowserWebUiBuiltFrontend", hostDoc, StringComparison.Ordinal);
        Assert.Contains("TestResults/browser-smoke", hostDoc, StringComparison.Ordinal);
        Assert.Contains("browser-smoke-artifacts", hostDoc, StringComparison.Ordinal);
        Assert.Contains("HTML/network diagnostics", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("screenshots", hostDoc, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("#705", readme, StringComparison.Ordinal);
        Assert.Contains("npm run verify", readme, StringComparison.Ordinal);
        Assert.Contains("Category=BrowserWebUiBuiltFrontend", readme, StringComparison.Ordinal);
        Assert.Contains("TestResults/browser-smoke", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalWebHostDocs_DocumentTypedBrowserApiContractWorkflow()
    {
        var hostDoc = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "local-web-host.md"));
        var readme = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "README.md"));

        Assert.Contains("#703", hostDoc, StringComparison.Ordinal);
        Assert.Contains("src/api/contracts.ts", hostDoc, StringComparison.Ordinal);
        Assert.Contains("src/api/client.ts", hostDoc, StringComparison.Ordinal);
        Assert.Contains("contract-fixtures", hostDoc, StringComparison.Ordinal);
        Assert.Contains("BrowserApiContractTests", hostDoc, StringComparison.Ordinal);
        Assert.Contains("BrowserCommandCoverageDto", hostDoc, StringComparison.Ordinal);
        Assert.Contains("npm run typecheck", hostDoc, StringComparison.Ordinal);
        Assert.Contains("playerMessage", hostDoc, StringComparison.Ordinal);
        Assert.Contains("technicalDetails", hostDoc, StringComparison.Ordinal);

        Assert.Contains("src/api/contracts.ts", readme, StringComparison.Ordinal);
        Assert.Contains("src/api/client.ts", readme, StringComparison.Ordinal);
        Assert.Contains("contract-fixtures", readme, StringComparison.Ordinal);
        Assert.Contains("BrowserApiContractTests", readme, StringComparison.Ordinal);
        Assert.Contains("BrowserApiClient", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalWebHostDocs_DocumentBrowserAudioSettingsWorkflow()
    {
        var hostDoc = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "local-web-host.md"));
        var readme = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "README.md"));

        Assert.Contains("#684", hostDoc, StringComparison.Ordinal);
        Assert.Contains("GET /api/audio/settings", hostDoc, StringComparison.Ordinal);
        Assert.Contains("POST /api/audio/settings", hostDoc, StringComparison.Ordinal);
        Assert.Contains("GET /api/audio/assets/{assetId}", hostDoc, StringComparison.Ordinal);
        Assert.Contains("shared `GameSettings` audio fields", hostDoc, StringComparison.Ordinal);
        Assert.Contains("autoplay", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Включить музыку в браузере", hostDoc, StringComparison.Ordinal);
        Assert.Contains("no local filesystem paths", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing audio files", hostDoc, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("#684", readme, StringComparison.Ordinal);
        Assert.Contains("getAudioSettings", readme, StringComparison.Ordinal);
        Assert.Contains("updateAudioSettings", readme, StringComparison.Ordinal);
        Assert.Contains("Включить музыку в браузере", readme, StringComparison.Ordinal);
        Assert.Contains("shared `GameSettings` audio fields", readme, StringComparison.Ordinal);
        Assert.Contains("no local filesystem paths", readme, StringComparison.OrdinalIgnoreCase);
    }
}
