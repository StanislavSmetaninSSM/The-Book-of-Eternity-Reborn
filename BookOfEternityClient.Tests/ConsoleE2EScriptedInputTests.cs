using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ConsoleE2EScriptedInputTests : IDisposable
{
    private readonly string _tempRoot;

    public ConsoleE2EScriptedInputTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "boe-e2e-scripted-input-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void FromJson_SupportsRequiredNavigationKeysAndPrintableText()
    {
        var scriptPath = WriteScript(
            """
            {
              "steps": [
                { "kind": "key", "key": "Up" },
                { "kind": "key", "key": "Down" },
                { "kind": "key", "key": "Left" },
                { "kind": "key", "key": "Right" },
                { "kind": "key", "key": "W" },
                { "kind": "key", "key": "S" },
                { "kind": "key", "key": "Enter" },
                { "kind": "key", "key": "Escape" },
                { "kind": "text", "text": "Мир" }
              ]
            }
            """);
        var artifactRoot = Path.Combine(_tempRoot, "artifacts");

        var input = ConsoleE2EScriptedInputSource.FromFile(scriptPath, artifactRoot);

        Assert.Equal(ConsoleKey.UpArrow, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.DownArrow, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.LeftArrow, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.RightArrow, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.W, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.S, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.Enter, input.ReadKey(intercept: true).Key);
        Assert.Equal(ConsoleKey.Escape, input.ReadKey(intercept: true).Key);
        Assert.Equal("Мир", input.ReadLine());
        input.AssertCompleted();
    }

    [Fact]
    public void ReadKey_WhenScriptIsExhausted_WritesFailureArtifactAndThrowsDiagnostic()
    {
        var scriptPath = WriteScript("{ \"steps\": [] }");
        var artifactRoot = Path.Combine(_tempRoot, "artifacts");
        var input = ConsoleE2EScriptedInputSource.FromFile(scriptPath, artifactRoot);

        var ex = Assert.Throws<ConsoleE2EScriptInputException>(() => input.ReadKey(intercept: true));

        Assert.Contains("exhausted", ex.Message, StringComparison.OrdinalIgnoreCase);
        var failurePath = Path.Combine(artifactRoot, "failure.txt");
        Assert.True(File.Exists(failurePath));
        var failureText = File.ReadAllText(failurePath);
        Assert.Contains("ReadKey", failureText, StringComparison.Ordinal);
        Assert.Contains(scriptPath, failureText, StringComparison.Ordinal);
    }

    [Fact]
    public void FromJson_InvalidJson_WritesFailureArtifactAndThrowsDiagnostic()
    {
        var scriptPath = WriteScript("{ invalid json");
        var artifactRoot = Path.Combine(_tempRoot, "invalid-json-artifacts");

        var ex = Assert.Throws<ConsoleE2EScriptInputException>(() =>
            ConsoleE2EScriptedInputSource.FromFile(scriptPath, artifactRoot));

        Assert.Equal(0, ex.NextStepIndex);
        var failurePath = Path.Combine(artifactRoot, "failure.txt");
        Assert.True(File.Exists(failurePath));
        Assert.Contains("Invalid console E2E script JSON", File.ReadAllText(failurePath), StringComparison.Ordinal);
    }

    [Fact]
    public void FromJson_InvalidKey_WritesFailureArtifactAndThrowsDiagnostic()
    {
        var scriptPath = WriteScript(
            """
            {
              "steps": [
                { "kind": "key", "key": "NotARealKey" }
              ]
            }
            """);
        var artifactRoot = Path.Combine(_tempRoot, "invalid-key-artifacts");

        var ex = Assert.Throws<ConsoleE2EScriptInputException>(() =>
            ConsoleE2EScriptedInputSource.FromFile(scriptPath, artifactRoot));

        Assert.Equal(0, ex.NextStepIndex);
        var failurePath = Path.Combine(artifactRoot, "failure.txt");
        Assert.True(File.Exists(failurePath));
        Assert.Contains("Unsupported console E2E key", File.ReadAllText(failurePath), StringComparison.Ordinal);
    }

    [Fact]
    public void ReadLine_TextStepDoesNotExposeFollowingControlKeyAsBufferedPaste()
    {
        var scriptPath = WriteScript(
            """
            {
              "steps": [
                { "kind": "text", "text": "Осматриваю горизонт" },
                { "kind": "key", "key": "Escape" }
              ]
            }
            """);
        var input = ConsoleE2EScriptedInputSource.FromFile(scriptPath, Path.Combine(_tempRoot, "text-then-key-artifacts"));

        Assert.Equal("Осматриваю горизонт", input.ReadLine());
        Assert.False(input.KeyAvailable);
        Assert.True(input.KeyAvailable);
        Assert.Equal(ConsoleKey.Escape, input.ReadKey(intercept: true).Key);
        input.AssertCompleted();
    }

    [Fact]
    public void ScriptedKeyEventsDriveMainMenuSelectionThroughProductionKeyHandler()
    {
        var scriptPath = WriteScript(
            """
            {
              "steps": [
                { "kind": "key", "key": "Down" },
                { "kind": "key", "key": "Enter" }
              ]
            }
            """);
        var input = ConsoleE2EScriptedInputSource.FromFile(scriptPath, Path.Combine(_tempRoot, "artifacts"));

        var afterDown = ConsoleMainMenuInputHandler.Apply(
            input.ReadKey(intercept: true),
            selectedIndex: 0,
            optionCount: 5);
        var afterEnter = ConsoleMainMenuInputHandler.Apply(
            input.ReadKey(intercept: true),
            selectedIndex: afterDown.SelectedIndex,
            optionCount: 5);

        Assert.Equal(1, afterDown.SelectedIndex);
        Assert.False(afterDown.ActivateSelection);
        Assert.Equal(1, afterEnter.SelectedIndex);
        Assert.True(afterEnter.ActivateSelection);
        input.AssertCompleted();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private string WriteScript(string json)
    {
        var path = Path.Combine(_tempRoot, "script.json");
        File.WriteAllText(path, json);
        return path;
    }
}
