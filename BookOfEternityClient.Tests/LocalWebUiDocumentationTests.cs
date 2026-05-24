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

        Assert.Contains("root page defaults to the player-facing main menu", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Advanced / developer panel", hostDoc, StringComparison.Ordinal);
        Assert.Contains("raw command console", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`/api/*` endpoint details", hostDoc, StringComparison.Ordinal);
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
        Assert.Contains("BrowserWebUiSmoke", checklist, StringComparison.Ordinal);
        Assert.Contains("BrowserWebUiParity", checklist, StringComparison.Ordinal);
    }
}
