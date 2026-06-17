param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

$sessionRoot = Join-Path $RepositoryRoot "BookOfEternityClient\game_session"
if (-not (Test-Path -LiteralPath $sessionRoot)) {
    throw "Local game_session was not found: $sessionRoot"
}

$jsonFiles = Get-ChildItem $sessionRoot -Recurse -File -Include *.json
foreach ($file in $jsonFiles) {
    try {
        Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json | Out-Null
    }
    catch {
        throw "Invalid JSON: $($file.FullName): $($_.Exception.Message)"
    }
}

$jsonlFiles = Get-ChildItem $sessionRoot -Recurse -File -Include *.jsonl
foreach ($file in $jsonlFiles) {
    $lineNo = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNo++
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        try {
            $line | ConvertFrom-Json | Out-Null
        }
        catch {
            throw "Invalid JSONL: $($file.FullName):${lineNo}: $($_.Exception.Message)"
        }
    }
}

Write-Host "JSON and JSONL syntax OK: $($jsonFiles.Count) json, $($jsonlFiles.Count) jsonl"

$tmp = Join-Path $env:TEMP ("boe-fixture-smoke-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null

try {
    dotnet new console --framework net8.0 --output $tmp --force | Out-Null
    $proj = Get-ChildItem -LiteralPath $tmp -Filter *.csproj | Select-Object -First 1 -ExpandProperty FullName
    dotnet add $proj reference (Join-Path $RepositoryRoot "BookOfEternityClient\BookOfEternityClient.csproj") | Out-Null

    $code = @'
using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var repo = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        var sourceSession = Path.Combine(repo, "BookOfEternityClient", "game_session");

        var realFs = new FileSystemManager(Path.Combine(repo, "BookOfEternityClient"), NullLogger<FileSystemManager>.Instance);
        var validator = new ValidationService(realFs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();
        Console.WriteLine($"VALIDATION ISSUES {issues.Count}");
        foreach (var issue in issues.Take(30))
            Console.WriteLine($"{issue.Severity} {issue.FilePath} {issue.Message}");

        var mortalDescriptors = ExplorerCommandCatalog.Descriptors
            .Where(d => d.Group == ExplorerCommandGroup.MortalWorld)
            .OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var aliasProblems = new List<string>();
        var renderProblems = new List<string>();
        var aliasCount = 0;
        var renderCount = 0;
        var console = new NullExplorerConsole();

        foreach (var descriptor in mortalDescriptors)
        {
            foreach (var alias in descriptor.Aliases)
            {
                aliasCount++;
                renderCount++;
                await SmokeCommandAsync(
                    sourceSession,
                    descriptor.Id,
                    alias,
                    aliasProblems,
                    renderProblems,
                    console);
            }
        }

        var universalCommands = new (string Id, string Command)[]
        {
            ("help", "/help"),
            ("status", "/status"),
            ("soul", "/soul"),
            ("achievements", "/achievements"),
            ("chronicle", "/chronicle"),
            ("story", "/story"),
            ("behavior", "/behavior"),
            ("lives", "/lives"),
            ("feathers", "/feathers"),
            ("codex", "/codex"),
            ("world_rules", "/world_rules"),
            ("gallery", "/gallery"),
            ("mods", "/mods"),
            ("validate", "/validate")
        };

        var universalProblems = new List<string>();
        foreach (var item in universalCommands)
        {
            renderCount++;
            await SmokeCommandAsync(
                sourceSession,
                item.Id,
                item.Command,
                universalProblems,
                renderProblems,
                console,
                printUniversal: true);
        }

        Console.WriteLine($"MORTAL ALIASES {aliasCount}");
        Console.WriteLine($"MORTAL PROBLEMS {aliasProblems.Count}");
        foreach (var problem in aliasProblems.Take(50))
            Console.WriteLine(problem);

        Console.WriteLine($"UNIVERSAL COMMANDS {universalCommands.Length}");
        Console.WriteLine($"UNIVERSAL PROBLEMS {universalProblems.Count}");
        foreach (var problem in universalProblems.Take(50))
            Console.WriteLine(problem);

        Console.WriteLine($"CONSOLE RENDER COMMANDS {renderCount}");
        Console.WriteLine($"CONSOLE RENDER PROBLEMS {renderProblems.Count}");
        foreach (var problem in renderProblems.Take(50))
            Console.WriteLine(problem);

        return issues.Count == 0 &&
               aliasProblems.Count == 0 &&
               universalProblems.Count == 0 &&
               renderProblems.Count == 0
            ? 0
            : 1;
    }

    private static async Task SmokeCommandAsync(
        string sourceSession,
        string id,
        string command,
        List<string> commandProblems,
        List<string> renderProblems,
        IExplorerConsole console,
        bool printUniversal = false)
    {
        var basePath = Path.Combine(Path.GetTempPath(), "boe-command-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(sourceSession, Path.Combine(basePath, "game_session"));

        try
        {
            var svc = await BuildServiceAsync(basePath);
            var result = await svc.ExecuteAsync(new ExplorerWebCommandRequest(command));
            var chars = JsonSerializer.Serialize(result).Length;
            if (printUniversal)
                Console.WriteLine($"UNIVERSAL {command} {result.State} blocks={result.Blocks.Count} actions={result.Actions.Count} prompts={result.Prompts.Count} chars={chars}");

            if (result.State is CommandExecutionState.Failed or CommandExecutionState.Blocked || chars <= 80)
                commandProblems.Add($"{id} {command}: state={result.State} blocks={result.Blocks.Count} actions={result.Actions.Count} prompts={result.Prompts.Count} chars={chars}");

            try
            {
                ExplorerCommandResultConsoleRenderer.Render(console, result);
            }
            catch (Exception ex)
            {
                renderProblems.Add($"{id} {command}: {ex.GetType().Name}: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            commandProblems.Add($"{id} {command}: EXCEPTION {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try { Directory.Delete(basePath, recursive: true); } catch { }
        }
    }

    private static async Task<ExplorerWebCommandService> BuildServiceAsync(string basePath)
    {
        var fs = new FileSystemManager(basePath, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        var settings = new GameSettings();
        var state = new StateManager(fs, settings, NullLogger<StateManager>.Instance);
        await state.LoadSettingsAsync();
        await state.RefreshGameStateAsync();
        var validation = new ValidationService(fs, NullLogger<ValidationService>.Instance);
        return new ExplorerWebCommandService(fs, state, new LocalizationManager(), validation);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}

internal sealed class NullExplorerConsole : IExplorerConsole
{
    public bool KeyAvailable => false;
    public void Clear() { }
    public void Write(IRenderable content) { }
    public void WriteLine() { }
    public void Markup(string markup) { }
    public void MarkupLine(string markup) { }
    public string? ReadLine() => null;
    public ConsoleKeyInfo ReadKey() => new(' ', ConsoleKey.Spacebar, false, false, false);
    public string Ask(string prompt, string defaultValue = "") => defaultValue;
    public bool Confirm(string prompt, bool defaultValue = false) => defaultValue;
    public T Prompt<T>(IPrompt<T> prompt) => throw new NotSupportedException();
}
'@

    Set-Content -LiteralPath (Join-Path $tmp "Program.cs") -Value $code -Encoding UTF8
    dotnet run --project $proj -- $RepositoryRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Fixture smoke runner failed with exit code $LASTEXITCODE"
    }
}
finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
