using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using System.Diagnostics;

namespace BookOfEternityClient.Tests;

/// <summary>Runs original clipboard/console consumers in a disposable, synthetic-only child.</summary>
public static class ClipboardConsoleProbe
{
    /// <summary>Fixture request; no live clipboard tool, provider or game loop is used.</summary>
    public sealed class Request
    {
        public string Mode { get; set; } = "service";
        public string Provider { get; set; } = "real";
        public string Text { get; set; } = "";
        public string Error { get; set; } = "Не удалось [прочитать] буфер.";
        public string DefaultValue { get; set; } = "";
        public string?[] Lines { get; set; } = [];
        public string ReaderMode { get; set; } = "text";
    }

    /// <summary>Calls the real service or original input entrypoint, recording only owned synthetic data.</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || args[2] != "clipboard") return 64;
        var root = Path.GetFullPath(args[0]);
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(Path.Combine(root, "request.json")))!;
        // Independent host lifetime exceeds the reader's in-process hard deadline.
        using var lifetime = new Timer(_ => Environment.Exit(91), null, TimeSpan.FromSeconds(12), Timeout.InfiniteTimeSpan);
        var input = new Input(request.Lines);
        var writer = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer)
        });
        Process? captureDeniedRoot = null;
        var captures = 0;
        IClipboardService original = request.Mode == "debt"
            ? new SystemClipboardService(NullLogger<SystemClipboardService>.Instance, process =>
            {
                if (++captures == 1) { captureDeniedRoot = process; return null; }
                return LinuxClipboardReaderIdentity.Capture(process);
            })
            : request.Provider == "real"
            ? new SystemClipboardService(NullLogger<SystemClipboardService>.Instance)
            : new Synthetic(request);
        var clipboard = new Counted(original);
        string? value = null, exception = null, draft = null;
        ClipboardReadResult? result = null;
        string[]? debtOutcomes = null;
        bool? originalAliveWithDebt = null, originalExitObserved = null;
        try
        {
            if (request.Mode == "debt")
            {
                var first = clipboard.TryReadText();
                originalAliveWithDebt = captureDeniedRoot != null && !captureDeniedRoot.HasExited;
                var second = clipboard.TryReadText();
                if (captureDeniedRoot == null) throw new InvalidOperationException("Missing original capture fixture root.");
                using var exitDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                await captureDeniedRoot.WaitForExitAsync(exitDeadline.Token);
                originalExitObserved = captureDeniedRoot.HasExited;
                request.ReaderMode = "text";
                File.WriteAllText(Path.Combine(root, "request.json"), JsonSerializer.Serialize(request));
                result = clipboard.TryReadText(); // explicit later gesture after actual exit
                debtOutcomes = [first.Outcome.ToString(), second.Outcome.ToString(), result.Value.Outcome.ToString()];
            }
            else if (request.Mode == "service") result = clipboard.TryReadText();
            else if (request.Mode == "ask")
                value = new SpectreExplorerConsole(clipboard, input).Ask("[cyan]Текст:[/]", request.DefaultValue);
            else if (request.Mode == "multiline")
                value = TextComposer.Read(new StandardTextComposerConsole(input), clipboard, new TextComposerOptions
                {
                    PromptMarkup = "[cyan]Текст:[/]", Mode = TextComposerMode.MultilineEditor,
                    DefaultValue = request.DefaultValue, PreserveNewlines = true
                });
            else if (request.Mode == "turn")
            {
                var fs = new FileSystemManager(Path.Combine(root, "game"), NullLogger<FileSystemManager>.Instance);
                var state = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);
                var engine = new GameEngine(fs: fs, stateManager: state, gameLoop: null!, normalizer: null!,
                    progressionSchedule: null!, ui: null!, explorer: null!, loc: new LocalizationManager(),
                    saveLoad: null!, imageService: null!, validator: null!, charService: null!, storyService: null!,
                    actorMemoryService: null!, audioService: null!, consoleAppearance: null!, systemModService: null!,
                    systemGuardianLibraryService: null!, criticalStateHealth: null!, worldDirectiveService: null!,
                    scenarioCoreService: null!, afterlifeArchiveCandidateService: null!, afterlifeReturnGuardService: null!,
                    rivalSoulArcService: null!, guardianCorrectionService: null!, pendingTurnState: null!, qteSceneService: null!,
                    clipboardService: clipboard, logger: NullLogger<GameEngine>.Instance, inputSource: input);
                var method = typeof(GameEngine).GetMethod("GetPlayerInput", BindingFlags.Instance | BindingFlags.NonPublic)!;
                value = await (Task<string>)method.Invoke(engine, null)!;
            }
            else throw new InvalidDataException("Unknown owned clipboard fixture mode.");
        }
        catch (Exception ex)
        {
            var actual = ex is TargetInvocationException { InnerException: not null } wrapped ? wrapped.InnerException! : ex;
            exception = actual.GetType().Name;
            draft = actual.GetType().GetProperty("Draft")?.GetValue(actual) as string;
        }
        var outcome = result.HasValue
            ? typeof(ClipboardReadResult).GetProperty("Outcome")?.GetValue(result.Value)?.ToString()
            : null;
        File.WriteAllText(Path.Combine(root, "probe.json"), JsonSerializer.Serialize(new
        {
            Value = value, Result = result, Outcome = outcome, Exception = exception, Draft = draft,
            ClipboardReads = clipboard.Reads, InputReads = input.Reads, Screen = writer.ToString(),
            Provider = request.Provider, Mode = request.Mode, HostPid = Environment.ProcessId
            , DebtOutcomes = debtOutcomes, OriginalAliveWithDebt = originalAliveWithDebt, OriginalExitObserved = originalExitObserved
        }));
        return 0;
    }

    private sealed class Counted(IClipboardService original) : IClipboardService
    {
        internal int Reads;
        public ClipboardReadResult TryReadText() { Reads++; return original.TryReadText(); }
    }

    private sealed class Synthetic(Request request) : IClipboardService
    {
        public ClipboardReadResult TryReadText() => request.Provider == "error"
            ? ClipboardReadResult.Fail(request.Error) : ClipboardReadResult.Ok(request.Text);
    }

    private sealed class Input(IEnumerable<string?> lines) : IConsoleInputSource
    {
        private readonly Queue<string?> _lines = new(lines);
        internal int Reads;
        public bool IsScripted => true;
        public bool KeyAvailable => false;
        public ConsoleKeyInfo ReadKey(bool intercept = true) => throw new InvalidOperationException("No synthetic key supplied.");
        public string? ReadLine()
        {
            Reads++;
            if (_lines.Count == 0) throw new InvalidOperationException("Owned input script exhausted; no implicit Enter.");
            return _lines.Dequeue();
        }
        public void AssertCompleted() { if (_lines.Count != 0) throw new InvalidOperationException("Owned input remains."); }
    }
}
