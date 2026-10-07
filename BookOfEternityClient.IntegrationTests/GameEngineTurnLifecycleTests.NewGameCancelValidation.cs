using System.Text.Json;
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
        public int EscapeReads { get; private set; }
        public bool IsScripted => true;
        public bool KeyAvailable => EscapeReads == 0;
        public ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            if (EscapeReads == 0) { EscapeReads++; return Key(ConsoleKey.Escape); }
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
