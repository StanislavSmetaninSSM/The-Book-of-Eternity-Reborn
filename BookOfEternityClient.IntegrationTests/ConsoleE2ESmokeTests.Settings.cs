using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ConsoleE2ESmokeTests
{
    private async Task<ConsoleE2ESandbox> CreateSettingsSandbox()
    {
        var root = FindRepositoryRoot();
        var sandbox = ConsoleE2ESandbox.CreateFromFixture(Path.Combine(root, "FileSystemExample", "game_session"), _tempRoot, true);
        var files = new FileSystemManager(sandbox.BasePath, NullLogger<FileSystemManager>.Instance);
        var settings = new GameSettings { Language = "ru", MusicEnabled = false, SoundEnabled = false, EnableQteEvents = true };
        var state = new StateManager(files, settings, NullLogger<StateManager>.Instance);
        await state.BootstrapLocalStorageAsync();
        File.WriteAllBytes(files.ResolvePath("config.json"), state.EncodeLocalSettings(settings));
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var prepared = await new LocalSettingsPreparation(files, state, new SystemModService(files, settings, NullLogger<SystemModService>.Instance))
            .PrepareAsync(lease, new(files.ReadExistingSessionGeneration(lease)!, File.ReadAllBytes(files.ResolvePath("config.json"))), settings);
        var result = await files.PublishLocalFilesAsync(lease, prepared.Changes);
        Assert.Equal(TrustedLocalPublicationDisposition.Committed, result.Disposition);
        return sandbox;
    }

    private Task<ConsoleClientRunResult> RunSettingsKeys(ConsoleE2ESandbox sandbox, string run, IEnumerable<string> keys)
    {
        var script = Path.Combine(sandbox.BasePath, run + ".json");
        File.WriteAllText(script, JsonSerializer.Serialize(new { steps = keys.Select(key => new { kind = "key", key }) }));
        return RunConsoleClient(FindRepositoryRoot(), sandbox.BasePath, script, Path.Combine(sandbox.BasePath, run));
    }

    private static readonly string[] EnterOptions = ["Up", "Up", "Up", "Enter"];
    private static readonly string[] LeaveOptionsAndExit = ["Escape", "Down", "Down", "Enter"];
    private static bool ReadQte(ConsoleE2ESandbox sandbox, bool projection)
    {
        var path = Path.Combine(sandbox.GameSessionPath, projection ? "game_state/core/game_settings.json" : "config.json");
        using var reader = new StreamReader(path);
        return JsonNode.Parse(reader.ReadToEnd())![projection ? "qteEventsEnabled" : "enableQteEvents"]!.GetValue<bool>();
    }

    [Fact]
    public async Task ConsoleSettings_ImmediateQteSaveSurvivesInterruptedMenuAndProcessRestart()
    {
        using var sandbox = await CreateSettingsSandbox();
        var toggle = EnterOptions.Concat(Enumerable.Repeat("Down", 4)).Append("Enter");
        var first = await RunSettingsKeys(sandbox, "first", toggle.Concat(LeaveOptionsAndExit));
        Assert.True(first.ExitCode == 0, first.Stderr + Tail(first.Stdout, 1500));
        Assert.False(ReadQte(sandbox, false)); Assert.False(ReadQte(sandbox, true));

        var interrupted = await RunSettingsKeys(sandbox, "interrupted", toggle);
        Assert.Equal(2, interrupted.ExitCode);
        Assert.Contains("exhausted", interrupted.Stderr, StringComparison.OrdinalIgnoreCase);
        // Inspect before any restart can repair a half-written settings set.
        Assert.True(ReadQte(sandbox, false)); Assert.True(ReadQte(sandbox, true));
        var afterInterruption = File.ReadAllBytes(Path.Combine(sandbox.GameSessionPath, "config.json"));
        var restarted = await RunSettingsKeys(sandbox, "restarted", EnterOptions.Concat(LeaveOptionsAndExit));
        Assert.True(restarted.ExitCode == 0, restarted.Stderr + Tail(restarted.Stdout, 1500));
        Assert.True(ReadQte(sandbox, false)); Assert.True(ReadQte(sandbox, true));
        Assert.Equal(afterInterruption, File.ReadAllBytes(Path.Combine(sandbox.GameSessionPath, "config.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsoleSettings_DiscardRestoresAcceptedLanguageAfterPreviewOrBlockedSave(bool otherOwner)
    {
        using var sandbox = await CreateSettingsSandbox();
        var config = Path.Combine(sandbox.GameSessionPath, "config.json"); var before = File.ReadAllBytes(config);
        if (otherOwner)
        {
            var files = new FileSystemManager(sandbox.BasePath, NullLogger<FileSystemManager>.Instance);
            var acquired = await new LocalUiSessionLockService(files).AcquireOrRefreshAsync(
                new("other-browser", "browser", "Other settings UI", TimeSpan.FromMinutes(5)), "Other settings operation");
            Assert.True(acquired.Acquired);
        }
        var keys = EnterOptions.Concat(Enumerable.Repeat("Down", 18)).Append("Enter"); // Language preview.
        if (otherOwner) keys = keys.Append("Escape"); // Save is blocked; the options draft stays visible.
        keys = keys.Concat(new[] { "Down", "Enter", "Down", "Down", "Enter" }); // Explicit reload/discard, then main-menu exit.
        var result = await RunSettingsKeys(sandbox, "discard", keys);
        Assert.True(result.ExitCode == 0, result.Stderr + Tail(result.Stdout, 2500));
        Assert.Equal(before, File.ReadAllBytes(config));
        var last = Directory.GetFiles(Path.Combine(sandbox.BasePath, "discard", "screens"), "*.json").OrderBy(path => path).Last();
        using var observation = JsonDocument.Parse(File.ReadAllText(last));
        Assert.Contains("Выход", observation.RootElement.GetProperty("selectedOption").GetString(), StringComparison.Ordinal);
        if (otherOwner) Assert.Contains("заблок", result.Stdout, StringComparison.OrdinalIgnoreCase);
    }
}
