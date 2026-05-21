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
}
