using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task OrdinaryPlayerCancel_RealStagingRetiresOnlyConsumedSnapshot()
    {
        var input = new NewGameCancelInput();
        var engine = await CreateOrdinaryCancelledBootstrapAsync(input);
        const string guardianPath = "game_state/meta/guardians.json";
        var baseline = await _fs.ReadFileBytesAsync(guardianPath);
        Assert.NotNull(baseline);
        var pending = await _fs.ReadFileBytesAsync(SystemGuardianLibraryService.AttractionRequestPath);
        Assert.Null(pending);
        Assert.DoesNotContain(await GetPrivateField<ValidationService>(engine, "_validator").ValidateGameStateAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var staged = false;
        input.Arm(() =>
        {
            Assert.True(File.Exists(_fs.ResolvePath("input/turn_request.json")));
            Assert.True(File.Exists(_fs.ResolvePath("game_state/control/pending_turn_snapshot.json")));
            staged = true;
        });
        await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Я осматриваю берег Моря Хаоса.")
            .WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Null(input.CallbackFailure);
        Assert.True(staged);
        Assert.Equal(1, input.EscapeReads);
        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.authority.json"));
        Assert.Equal(baseline, await _fs.ReadFileBytesAsync(guardianPath));
        Assert.Equal(pending, await _fs.ReadFileBytesAsync(SystemGuardianLibraryService.AttractionRequestPath));
        Assert.DoesNotContain(await GetPrivateField<ValidationService>(engine, "_validator").ValidateGameStateAsync(),
            issue => issue.Severity == IssueSeverity.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualCancellation_RestoreFailurePreservesPendingAuthorityAndEvidence(bool ordinaryPlayer)
    {
        var input = new NewGameCancelInput();
        var engine = await CreateOrdinaryBootstrapAsync(input);
        if (ordinaryPlayer)
            Assert.False(await InvokePrivateAsync<bool>(engine, "WaitForGmResponse").WaitAsync(TimeSpan.FromSeconds(8)));
        var expected = new Dictionary<string, byte[]>();
        input.Arm(() =>
        {
            const string manifestPath = "game_state/control/pending_turn_snapshot.json";
            const string authorityPath = "game_state/control/pending_turn_snapshot.authority.json";
            Assert.True(File.Exists(_fs.ResolvePath("input/turn_request.json")));
            var manifest = File.ReadAllBytes(_fs.ResolvePath(manifestPath));
            var node = JsonNode.Parse(manifest)!.AsObject();
            var backups = node["rollbackBackups"]!.AsObject().Select(pair => pair.Value!.GetValue<string>()).ToArray();
            Assert.NotEmpty(backups);
            string OwnBackup(string path)
            {
                var physical = Path.GetFullPath(_fs.ResolvePath(path));
                Assert.StartsWith(Path.GetFullPath(_rootPath) + Path.DirectorySeparatorChar, physical, StringComparison.Ordinal);
                return physical;
            }
            File.WriteAllText(OwnBackup(backups[0]), "deliberately corrupted own evidence");
            expected.Add(manifestPath, manifest);
            expected.Add(authorityPath, File.ReadAllBytes(_fs.ResolvePath(authorityPath)));
            foreach (var backup in backups)
                expected.Add(backup, File.ReadAllBytes(OwnBackup(backup)));
        });
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (ordinaryPlayer)
                await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Я осматриваю берег Моря Хаоса.")
                    .WaitAsync(TimeSpan.FromSeconds(8));
            else
                await InvokePrivateAsync<bool>(engine, "WaitForGmResponse").WaitAsync(TimeSpan.FromSeconds(8));
        });
        Assert.Equal(1, input.EscapeReads);
        Assert.Null(input.CallbackFailure);
        Assert.IsType<InvalidDataException>(failure);
        Assert.NotEmpty(expected);
        foreach (var (path, bytes) in expected)
            Assert.Equal(bytes, await _fs.ReadFileBytesAsync(path));
    }

    private async Task<GameEngine> CreateOrdinaryCancelledBootstrapAsync(NewGameCancelInput input)
    {
        var engine = await CreateOrdinaryBootstrapAsync(input);
        Assert.False(await InvokePrivateAsync<bool>(engine, "WaitForGmResponse").WaitAsync(TimeSpan.FromSeconds(8)));
        return engine;
    }

    private async Task<GameEngine> CreateOrdinaryBootstrapAsync(NewGameCancelInput input)
    {
        var engine = CreateGameEngine(input, settings => { settings.MusicEnabled = false; settings.SoundEnabled = false; });
        await GetPrivateField<StateManager>(engine, "_stateManager").BootstrapLocalStorageAsync();
        var guardian = new SystemGuardianLibraryService(_fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SystemGuardianLibraryService>.Instance)
            .BuildFreeformPendingGuardianCreationNode(
                "Спокойный Хранитель маяка, который встречает душу на берегу Моря Хаоса.", "Пробная Душа");
        await InvokePrivateAsync<string>(engine, "InitializeChaosSea", "Пробная Душа",
            "Человеческий силуэт мягкого синего света.", guardian, null);
        return engine;
    }

    [Fact]
    public async Task OrdinaryNewGameCancel_RealBootstrapRetainsValidPlayerPreflight()
    {
        var input = new NewGameCancelInput();
        var logger = new NewGameCancelLogger();
        var engine = CreateGameEngine(input, settings =>
        {
            settings.MusicEnabled = false;
            settings.SoundEnabled = false;
        }, logger: logger);
        // Ordinary RunAsync bootstraps local settings before NewGameFlow.
        await GetPrivateField<StateManager>(engine, "_stateManager").BootstrapLocalStorageAsync();
        var guardian = new SystemGuardianLibraryService(_fs,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SystemGuardianLibraryService>.Instance)
            .BuildFreeformPendingGuardianCreationNode(
                "Спокойный Хранитель маяка, который встречает душу на берегу Моря Хаоса.", "Пробная Душа");
        await InvokePrivateAsync<string>(engine, "InitializeChaosSea", "Пробная Душа",
            "Человеческий силуэт мягкого синего света.", guardian, null);
        var validator = GetPrivateField<ValidationService>(engine, "_validator");
        var before = await validator.ValidateGameStateAsync();
        Assert.True(_fs.FileExists("input/turn_request.json"));
        Assert.False(await InvokePrivateAsync<bool>(engine, "WaitForGmResponse").WaitAsync(TimeSpan.FromSeconds(8)));
        Assert.Equal(1, input.EscapeReads);
        Assert.False(_fs.FileExists("input/turn_request.json"));
        var after = await validator.ValidateGameStateAsync();
        var accepted = await InvokePrivateAsync<bool>(engine, "ValidateCurrentGameStateOrShowErrorsAsync", "перед отправкой хода");
        var report = JsonSerializer.Serialize(new { Before = before, After = after, PreflightAccepted = accepted,
            input.EscapeReads, logger.Messages });
        _directGachaOutput?.WriteLine(report);
        var folder = Path.Combine(TestRepoPaths.RepoRoot, "TestResults/newgame-cancel", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "diagnostic.json"), report);
        Assert.True(accepted && after.All(issue => issue.Severity != IssueSeverity.Error), report);
    }

    private sealed class NewGameCancelInput : IConsoleInputSource
    {
        private Action? _beforeEscape;
        public Exception? CallbackFailure { get; private set; }
        public void Arm(Action beforeEscape) { EscapeReads = 0; CallbackFailure = null; _beforeEscape = beforeEscape; }
        public int EscapeReads { get; private set; }
        public bool IsScripted => true;
        public bool KeyAvailable => EscapeReads == 0;
        public ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            if (EscapeReads == 0)
            {
                EscapeReads++;
                try { _beforeEscape?.Invoke(); }
                catch (Exception failure) { CallbackFailure = failure; }
                return Key(ConsoleKey.Escape);
            }
            return Key(ConsoleKey.Enter);
        }
        public string? ReadLine() => null;
        public void AssertCompleted() { }
    }

    private sealed class NewGameCancelLogger : ILogger<GameEngine>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? failure,
            Func<TState, Exception?, string> format) => Messages.Add(format(state, failure) + failure);
    }
}
