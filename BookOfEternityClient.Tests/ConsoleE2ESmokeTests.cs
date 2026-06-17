using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ConsoleE2ESmokeTests : IDisposable
{
#if DEBUG
    private const string TestBuildConfiguration = "Debug";
#else
    private const string TestBuildConfiguration = "Release";
#endif

    private readonly string _tempRoot;

    public ConsoleE2ESmokeTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "boe-console-e2e-smoke-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task ConsoleE2E_MainMenuExitScript_UsesSandboxAndEmitsObservationArtifacts()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureGameSessionPath = Path.Combine(repoRoot, "FileSystemExample", "game_session");
        using var sandbox = ConsoleE2ESandbox.CreateFromFixture(
            fixtureGameSessionPath,
            _tempRoot,
            preserveArtifacts: true);

        var artifactRoot = Path.Combine(sandbox.BasePath, "artifacts");
        var scriptPath = Path.Combine(sandbox.BasePath, "input-script.json");
        File.WriteAllText(
            scriptPath,
            """
            {
              "steps": [
                { "kind": "key", "key": "Up" },
                { "kind": "key", "key": "Enter" }
              ]
            }
            """);

        var result = await RunConsoleClient(repoRoot, sandbox.BasePath, scriptPath, artifactRoot);

        Assert.True(
            result.ExitCode == 0,
            $"Console E2E smoke process exited with {result.ExitCode}.{Environment.NewLine}STDERR:{Environment.NewLine}{result.Stderr}{Environment.NewLine}STDOUT tail:{Environment.NewLine}{Tail(result.Stdout, 2000)}");
        Assert.False(File.Exists(Path.Combine(artifactRoot, "failure.txt")));

        var screenDirectory = Path.Combine(artifactRoot, "screens");
        Assert.True(Directory.Exists(screenDirectory), "Expected scripted run to create a screens artifact directory.");
        var jsonSnapshots = Directory.GetFiles(screenDirectory, "*.json").OrderBy(path => path).ToArray();
        Assert.NotEmpty(jsonSnapshots);

        using var firstSnapshot = JsonDocument.Parse(File.ReadAllText(jsonSnapshots[0]));
        Assert.Equal("menu", firstSnapshot.RootElement.GetProperty("inputMode").GetString());
        Assert.Equal(1, firstSnapshot.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.True(firstSnapshot.RootElement.GetProperty("options").GetArrayLength() >= 5);

        using var lastSnapshot = JsonDocument.Parse(File.ReadAllText(jsonSnapshots[^1]));
        Assert.Equal("exit", lastSnapshot.RootElement.GetProperty("inputMode").GetString());
        var selectedOption = lastSnapshot.RootElement.GetProperty("selectedOption").GetString();
        Assert.True(
            selectedOption?.Contains("Выход", StringComparison.Ordinal) == true ||
            selectedOption?.Contains("Exit", StringComparison.Ordinal) == true,
            $"Expected exit option to be selected, got '{selectedOption}'.");
    }

    [Fact]
    public async Task ConsoleE2E_OptionsMenuScript_UsesScriptedInputAndEmitsMenuObservations()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureGameSessionPath = Path.Combine(repoRoot, "FileSystemExample", "game_session");
        using var sandbox = ConsoleE2ESandbox.CreateFromFixture(
            fixtureGameSessionPath,
            _tempRoot,
            preserveArtifacts: true);

        var artifactRoot = Path.Combine(sandbox.BasePath, "artifacts");
        var scriptPath = Path.Combine(sandbox.BasePath, "input-script.json");
        File.WriteAllText(
            scriptPath,
            """
            {
              "steps": [
                { "kind": "key", "key": "Up" },
                { "kind": "key", "key": "Up" },
                { "kind": "key", "key": "Up" },
                { "kind": "key", "key": "Enter" },
                { "kind": "key", "key": "Escape" },
                { "kind": "key", "key": "Down" },
                { "kind": "key", "key": "Down" },
                { "kind": "key", "key": "Enter" }
              ]
            }
            """);

        var result = await RunConsoleClient(repoRoot, sandbox.BasePath, scriptPath, artifactRoot);

        Assert.True(
            result.ExitCode == 0,
            $"Console E2E options smoke process exited with {result.ExitCode}.{Environment.NewLine}STDERR:{Environment.NewLine}{result.Stderr}{Environment.NewLine}STDOUT tail:{Environment.NewLine}{Tail(result.Stdout, 2000)}");

        var screenDirectory = Path.Combine(artifactRoot, "screens");
        var jsonSnapshots = Directory.GetFiles(screenDirectory, "*.json").OrderBy(path => path).ToArray();
        var optionsSnapshotPath = Assert.Single(
            jsonSnapshots,
            path => Path.GetFileName(path).Contains("options-menu", StringComparison.OrdinalIgnoreCase));
        using var optionsSnapshot = JsonDocument.Parse(File.ReadAllText(optionsSnapshotPath));
        Assert.Equal("menu", optionsSnapshot.RootElement.GetProperty("inputMode").GetString());
        Assert.Contains("Опции", optionsSnapshot.RootElement.GetProperty("screenTitle").GetString(), StringComparison.Ordinal);
        Assert.True(optionsSnapshot.RootElement.GetProperty("options").GetArrayLength() >= 5);
    }

    [Fact]
    public async Task ConsoleE2E_StatusCommand_RealSpectreRenderReturnsAfterKeyPrompt()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureGameSessionPath = Path.Combine(repoRoot, "FileSystemExample", "game_session");
        using var sandbox = ConsoleE2ESandbox.CreateFromFixture(
            fixtureGameSessionPath,
            _tempRoot,
            preserveArtifacts: true);

        var artifactRoot = Path.Combine(sandbox.BasePath, "artifacts");
        var scriptPath = Path.Combine(sandbox.BasePath, "status-script.json");
        File.WriteAllText(
            scriptPath,
            """
            {
              "steps": [
                { "kind": "key", "key": "Enter" },
                { "kind": "text", "text": "/статус" },
                { "kind": "key", "key": "Enter" },
                { "kind": "key", "key": "Enter" }
              ]
            }
            """);

        var result = await RunConsoleClient(
            repoRoot,
            sandbox.BasePath,
            scriptPath,
            artifactRoot,
            timeout: TimeSpan.FromSeconds(20));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Console E2E scripted input failed at step 4", result.Stderr, StringComparison.Ordinal);
        Assert.Contains("requested text input", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Статус", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Здоров", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("Деньги", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsoleE2E_ExhaustedScript_PreservesRealRunnerFailureArtifacts()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureGameSessionPath = Path.Combine(repoRoot, "FileSystemExample", "game_session");
        using var sandbox = ConsoleE2ESandbox.CreateFromFixture(
            fixtureGameSessionPath,
            _tempRoot,
            preserveArtifacts: true);

        var artifactRoot = Path.Combine(sandbox.BasePath, "artifacts");
        var scriptPath = Path.Combine(sandbox.BasePath, "input-script.json");
        File.WriteAllText(scriptPath, "{ \"steps\": [] }");

        var result = await RunConsoleClient(repoRoot, sandbox.BasePath, scriptPath, artifactRoot);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Console E2E scripted input failed at step 0", result.Stderr, StringComparison.Ordinal);

        var failurePath = Path.Combine(artifactRoot, "failure.txt");
        Assert.True(File.Exists(failurePath), "Expected the real scripted runner to preserve failure.txt.");
        Assert.Contains("exhausted", File.ReadAllText(failurePath), StringComparison.OrdinalIgnoreCase);

        var screenDirectory = Path.Combine(artifactRoot, "screens");
        var jsonSnapshots = Directory.GetFiles(screenDirectory, "*.json").OrderBy(path => path).ToArray();
        Assert.Contains(jsonSnapshots, path => Path.GetFileName(path).Contains("error", StringComparison.OrdinalIgnoreCase));

        var errorSnapshotPath = jsonSnapshots.Single(path => Path.GetFileName(path).Contains("error", StringComparison.OrdinalIgnoreCase));
        using var errorSnapshot = JsonDocument.Parse(File.ReadAllText(errorSnapshotPath));
        Assert.Equal("error", errorSnapshot.RootElement.GetProperty("inputMode").GetString());
        Assert.Equal("ConsoleE2EScriptInputException", errorSnapshot.RootElement.GetProperty("errorType").GetString());
        Assert.Contains("exhausted", errorSnapshot.RootElement.GetProperty("errorMessage").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConsoleE2E_InvalidScriptJson_ReturnsExit2AndFailureArtifactBeforeHostStartup()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureGameSessionPath = Path.Combine(repoRoot, "FileSystemExample", "game_session");
        using var sandbox = ConsoleE2ESandbox.CreateFromFixture(
            fixtureGameSessionPath,
            _tempRoot,
            preserveArtifacts: true);

        var artifactRoot = Path.Combine(sandbox.BasePath, "artifacts");
        var scriptPath = Path.Combine(sandbox.BasePath, "input-script.json");
        File.WriteAllText(scriptPath, "{ invalid json");

        var result = await RunConsoleClient(repoRoot, sandbox.BasePath, scriptPath, artifactRoot);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid console E2E script JSON", result.Stderr, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(artifactRoot, "failure.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "FileSystemExample", "game_session")) &&
                File.Exists(Path.Combine(dir.FullName, "BookOfEternityClient", "BookOfEternityClient.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root for console E2E smoke test.");
    }

    private static async Task<ConsoleClientRunResult> RunConsoleClient(
        string repoRoot,
        string sandboxBasePath,
        string scriptPath,
        string artifactRoot,
        TimeSpan? timeout = null)
    {
        var stdoutPath = Path.Combine(sandboxBasePath, "stdout.txt");
        var stderrPath = Path.Combine(sandboxBasePath, "stderr.txt");
        var projectPath = Path.Combine(repoRoot, "BookOfEternityClient", "BookOfEternityClient.csproj");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false
            }
        };
        process.StartInfo.ArgumentList.Add("run");
        process.StartInfo.ArgumentList.Add("--project");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.ArgumentList.Add("--configuration");
        process.StartInfo.ArgumentList.Add(TestBuildConfiguration);
        process.StartInfo.ArgumentList.Add("--no-build");
        process.StartInfo.ArgumentList.Add("--no-restore");
        process.StartInfo.ArgumentList.Add("--");
        process.StartInfo.ArgumentList.Add(sandboxBasePath);
        process.StartInfo.ArgumentList.Add("--e2e-script");
        process.StartInfo.ArgumentList.Add(scriptPath);
        process.StartInfo.ArgumentList.Add("--e2e-artifacts");
        process.StartInfo.ArgumentList.Add(artifactRoot);
        process.StartInfo.ArgumentList.Add("--plain-output");
        process.StartInfo.Environment["NO_COLOR"] = "1";

        Assert.True(process.Start(), "Failed to start dotnet run for console E2E smoke test.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var waitTask = process.WaitForExitAsync();
        var finished = await Task.WhenAny(waitTask, Task.Delay(timeout ?? TimeSpan.FromSeconds(45)));
        if (finished != waitTask)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort cleanup; the assertion below records the timeout.
            }

            Assert.Fail($"Console E2E smoke test timed out. Sandbox: {sandboxBasePath}");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        File.WriteAllText(stdoutPath, stdout);
        File.WriteAllText(stderrPath, stderr);
        return new ConsoleClientRunResult(process.ExitCode, stdout, stderr);
    }

    private static string Tail(string value, int maxCharacters)
    {
        if (value.Length <= maxCharacters)
            return value;

        return value[^maxCharacters..];
    }

    private sealed record ConsoleClientRunResult(int ExitCode, string Stdout, string Stderr);
}
