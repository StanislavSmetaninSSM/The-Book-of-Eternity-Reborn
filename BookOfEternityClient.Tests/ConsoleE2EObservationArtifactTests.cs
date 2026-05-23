using System.Text.Json;
using BookOfEternityClient.Configuration;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ConsoleE2EObservationArtifactTests : IDisposable
{
    private readonly string _artifactRoot;

    public ConsoleE2EObservationArtifactTests()
    {
        _artifactRoot = Path.Combine(Path.GetTempPath(), "boe-e2e-observation-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_artifactRoot);
    }

    [Fact]
    public void WriteSnapshot_WritesDeterministicMainMenuTextAndJsonArtifacts()
    {
        var writer = new ConsoleE2EObservationArtifactWriter(_artifactRoot, runId: "run-main-menu");
        var snapshot = new ConsoleE2EObservationSnapshot(
            RunId: "run-main-menu",
            StepIndex: 0,
            CapturedAtUtc: new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero),
            InputMode: ConsoleE2EInputMode.Menu,
            ScreenTitle: "Главное меню",
            PlayerFacingText: "Добро пожаловать в Книгу Вечности",
            Options: ["Продолжить", "Об игре", "Выход"],
            SelectedOption: "Продолжить",
            ArtifactRoot: _artifactRoot,
            LogPath: Path.Combine(_artifactRoot, "stdout.txt"));

        var artifact = writer.WriteSnapshot(snapshot, slug: "main-menu");

        Assert.True(File.Exists(artifact.TextPath));
        Assert.True(File.Exists(artifact.JsonPath));

        var text = File.ReadAllText(artifact.TextPath);
        Assert.Contains("Главное меню", text, StringComparison.Ordinal);
        Assert.Contains("inputMode: menu", text, StringComparison.Ordinal);
        Assert.Contains("selectedOption: Продолжить", text, StringComparison.Ordinal);
        Assert.Contains("- Продолжить", text, StringComparison.Ordinal);
        Assert.Contains("Добро пожаловать", text, StringComparison.Ordinal);

        using var json = JsonDocument.Parse(File.ReadAllText(artifact.JsonPath));
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("menu", json.RootElement.GetProperty("inputMode").GetString());
        Assert.Equal("Главное меню", json.RootElement.GetProperty("screenTitle").GetString());
        Assert.Equal("Продолжить", json.RootElement.GetProperty("selectedOption").GetString());
    }

    [Fact]
    public void WriteSnapshot_DoesNotInventOrLeakHiddenInternalState()
    {
        const string hiddenSecret = "GM_INTERNAL_SECRET_SHOULD_NOT_LEAK";
        var writer = new ConsoleE2EObservationArtifactWriter(_artifactRoot, runId: "run-hidden");
        var snapshot = new ConsoleE2EObservationSnapshot(
            RunId: "run-hidden",
            StepIndex: 1,
            CapturedAtUtc: DateTimeOffset.UnixEpoch,
            InputMode: ConsoleE2EInputMode.TextPrompt,
            ScreenTitle: "Действие игрока",
            PlayerFacingText: "Что вы делаете дальше?",
            Options: [],
            SelectedOption: null,
            ArtifactRoot: _artifactRoot,
            LogPath: null);

        var artifact = writer.WriteSnapshot(snapshot, slug: "prompt");

        Assert.DoesNotContain(hiddenSecret, File.ReadAllText(artifact.TextPath), StringComparison.Ordinal);
        Assert.DoesNotContain(hiddenSecret, File.ReadAllText(artifact.JsonPath), StringComparison.Ordinal);
        Assert.DoesNotContain("internal", File.ReadAllText(artifact.JsonPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteExceptionSnapshot_PreservesErrorArtifactsForFailures()
    {
        var writer = new ConsoleE2EObservationArtifactWriter(_artifactRoot, runId: "run-error");
        var exception = new InvalidOperationException("Scripted input timed out waiting for prompt.");

        var artifact = writer.WriteExceptionSnapshot(
            stepIndex: 2,
            screenTitle: "Timeout",
            playerFacingText: "The console E2E run timed out before the next prompt.",
            exception: exception,
            slug: "timeout");

        var text = File.ReadAllText(artifact.TextPath);
        Assert.Contains("inputMode: error", text, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", text, StringComparison.Ordinal);
        Assert.Contains("Scripted input timed out", text, StringComparison.Ordinal);

        using var json = JsonDocument.Parse(File.ReadAllText(artifact.JsonPath));
        Assert.Equal("error", json.RootElement.GetProperty("inputMode").GetString());
        Assert.Equal("InvalidOperationException", json.RootElement.GetProperty("errorType").GetString());
        Assert.Contains("timed out", json.RootElement.GetProperty("errorMessage").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ObservationArtifactFormatIsDocumentedForAgents()
    {
        var doc = ReadRepoFile("docs", "e2e", "console-observation-artifacts.md");

        foreach (var requiredText in new[]
        {
            "Issue: #677",
            "ConsoleE2EObservationArtifactWriter",
            "schemaVersion",
            "inputMode",
            "playerFacingText",
            "selectedOption",
            "errorType",
            "screens/<step>-<slug>.json",
            "screens/<step>-<slug>.txt",
            "hidden/internal-only state"
        })
        {
            Assert.Contains(requiredText, doc, StringComparison.Ordinal);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_artifactRoot))
            Directory.Delete(_artifactRoot, recursive: true);
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
