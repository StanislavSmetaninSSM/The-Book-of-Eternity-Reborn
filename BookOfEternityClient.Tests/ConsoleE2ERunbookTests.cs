using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ConsoleE2ERunbookTests
{
    [Fact]
    public void AgentRunbookDocumentsRequiredWorkflowAndSafetyRules()
    {
        var runbook = ReadRepoFile("docs", "e2e", "console-agent-runbook.md");

        foreach (var requiredText in new[]
        {
            "Issue: #679",
            "FileSystemExample/game_session",
            "ConsoleE2ESandbox.CreateFromFixture",
            "dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter ConsoleE2E",
            "--e2e-script",
            "--e2e-artifacts",
            "--plain-output",
            "kind",
            "key",
            "text",
            "preserveArtifacts: true",
            "tracked GitHub issue",
            "Mortal World mechanics",
            "Afterlife contract",
            "docs/console-e2e-sandbox.md"
        })
        {
            Assert.Contains(requiredText, runbook, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AgentRunbookCoversCommonConsoleE2ETroubleshootingCases()
    {
        var runbook = ReadRepoFile("docs", "e2e", "console-agent-runbook.md");

        foreach (var requiredText in new[]
        {
            "invalid `game_session`",
            "prompt/input hang",
            "timeout",
            "ANSI",
            "NO_COLOR",
            "cleanup",
            "screen/state snapshots",
            "failure artifacts",
            "Console E2E scripted input failed at step 0",
            "$RUN_ROOT/artifacts/failure.txt",
            "screens/*error*.json"
        })
        {
            Assert.Contains(requiredText, runbook, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadRepoFile(params string[] relativePathParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativePathParts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate repository file: " + Path.Combine(relativePathParts));
    }
}
