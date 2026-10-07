using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

[CollectionDefinition("desktop-helper-console", DisableParallelization = true)]
public sealed class DesktopHelperConsoleCollection { }

internal sealed class DesktopHelpersFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "boe-desktop-" + Guid.NewGuid().ToString("N"), "Тест [red]🌌 путь");
    public readonly GameSettings Settings = new() { MusicEnabled = false, SoundEnabled = false };
    public readonly FileSystemManager Files;
    public readonly StateManager State;
    public readonly SystemModService Mods;
    public readonly List<ProcessStartInfo> Requests = [];
    public readonly DesktopPathOpener Opener;
    public readonly ImageService Images;
    public readonly StringWriter Output = new();
    private readonly IAnsiConsole _previous;
    private readonly AudioService _audio;
    public DesktopHelpersFixture(string launch = "requested")
    {
        Directory.CreateDirectory(Root);
        Files = new(Root, NullLogger<FileSystemManager>.Instance);
        State = new(Files, Settings, NullLogger<StateManager>.Instance);
        Mods = new(Files, Settings, NullLogger<SystemModService>.Instance);
        Opener = new(start =>
        {
            Requests.Add(start);
            if (launch == "unavailable") throw new Win32Exception(2, "synthetic association unavailable");
            if (launch == "failed") throw new InvalidOperationException("synthetic failure");
            if (launch == "cancelled") throw new OperationCanceledException("synthetic cancelled");
        });
        _audio = AudioService.CreateBrowserManaged(Files, Settings, NullLogger<AudioService>.Instance); // inert, no backend/device query
        Images = new(Files, Settings, new(), NullLogger<ImageService>.Instance, Opener);
        _previous = AnsiConsole.Console;
        AnsiConsole.Console = CreateConsole(Output);
    }
    internal static IAnsiConsole CreateConsole(StringWriter output)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(output) });
        console.Profile.Width = 240; return console;
    }
    public string Image(string name = "картинка 🌌.png") { var p = Path.Combine(Root, name); File.WriteAllBytes(p, [1, 2, 3]); return p; }
    public GameEngine Engine(IConsoleInputSource? input = null) => new(fs: Files, stateManager: State, gameLoop: null!, normalizer: null!, progressionSchedule: null!,
        ui: null!, explorer: null!, loc: new(), saveLoad: null!, imageService: Images, validator: null!, charService: null!, storyService: null!, actorMemoryService: null!,
        audioService: _audio, consoleAppearance: new(Settings, NullLogger<ConsoleAppearanceService>.Instance), systemModService: Mods,
        systemGuardianLibraryService: null!, criticalStateHealth: null!, worldDirectiveService: null!, scenarioCoreService: null!, afterlifeArchiveCandidateService: null!,
        afterlifeReturnGuardService: null!, rivalSoulArcService: null!, guardianCorrectionService: null!, pendingTurnState: null!, qteSceneService: null!, clipboardService: null!,
        logger: NullLogger<GameEngine>.Instance, inputSource: input, desktopPathOpener: Opener);
    public ExplorerMode Explorer(DesktopExplorerConsole console) => new(State, Files, new(), imageService: Images, systemModService: Mods, console: console, desktopPathOpener: Opener);
    public static object? Invoke(object target, string method, params object?[] args)
    {
        try { return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args); }
        catch (TargetInvocationException ex) when (ex.InnerException is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
    public void ExactRequest(string path)
    {
        var request = Assert.Single(Requests);
        Assert.Equal(path, request.FileName); Assert.True(request.UseShellExecute); Assert.Empty(request.ArgumentList); Assert.Equal("", request.Arguments);
    }
    public void Dispose()
    {
        AnsiConsole.Console = _previous;
        _audio.DisposeAsync().AsTask().GetAwaiter().GetResult();
        var own = Directory.GetParent(Root)!.FullName;
        Directory.Delete(own, true);
        Assert.False(Directory.Exists(own)); // no child/process launched; adapter only
    }
}

internal sealed class DesktopInput(params ConsoleKey[] keys) : IConsoleInputSource
{
    private readonly Queue<ConsoleKey> _keys = new(keys);
    public int Reads { get; private set; }
    public bool IsScripted => true;
    public bool KeyAvailable => _keys.Count > 0;
    public ConsoleKeyInfo ReadKey(bool intercept = true) { Reads++; return new('\0', _keys.Dequeue(), false, false, false); }
    public string? ReadLine() => throw new NotSupportedException();
    public void AssertCompleted() => Assert.Empty(_keys);
}

internal sealed class DesktopExplorerConsole(params string[] choices) : IExplorerConsole
{
    public readonly StringWriter Output = new();
    private readonly Queue<string> _choices = new(choices);
    public readonly List<string> PromptScreens = [];
    public int KeyReads { get; private set; }
    public DesktopExplorerConsole() : this([]) { }
    private IAnsiConsole Console => DesktopHelpersFixture.CreateConsole(Output);
    public void Clear() => Output.GetStringBuilder().Clear();
    public void Write(IRenderable content) => Console.Write(content);
    public void Markup(string markup) => Console.Markup(markup);
    public void MarkupLine(string markup) => Console.MarkupLine(markup);
    public void WriteLine() => Output.WriteLine();
    public string Ask(string prompt, string defaultValue = "") => throw new NotSupportedException();
    public bool Confirm(string prompt, bool defaultValue = false) => throw new NotSupportedException();
    public T Prompt<T>(IPrompt<T> prompt) { PromptScreens.Add(Output.ToString()); return (T)(object)_choices.Dequeue(); }
    public string? ReadLine() => throw new NotSupportedException();
    public bool KeyAvailable => false;
    public ConsoleKeyInfo ReadKey() { KeyReads++; return new('\r', ConsoleKey.Enter, false, false, false); }
}
