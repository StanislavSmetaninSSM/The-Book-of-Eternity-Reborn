using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>Exercises the ordinary console Save boundary against a genuine active hosted main.</summary>
    [Fact]
    public async Task ManualSave_ActiveHostedMainWithoutAmbientGrantPublishesAndCloses()
    {
        Assert.True(OperatingSystem.IsLinux());
        var own = Path.Combine("/tmp", "ms-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        _directGachaOutput?.WriteLine("Owned manual Save evidence: " + own);
        var package = Path.Combine(own, "package");
        await RunBrowserColdChildAsync("pwsh", ["-NoLogo", "-NoProfile", "-File",
            Path.Combine(TestRepoPaths.RepoRoot, "scripts/build-linux-supervisor.ps1"),
            "-OutputDirectory", package, "-IncludeHostGuardian", "-IncludeTerminalFixture"],
            Path.Combine(own, "native.log"), 25);
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!,
            "BookOfEternityClient.TestSupport.dll");
        await RunBrowserColdChildAsync(Path.Combine(package, "host-guardian"), ["--live-turn",
            Path.Combine(own, "guardian.json"), "25000",
            Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"), support,
            "engine-manual-save-active", typeof(GameEngineTurnLifecycleTests).Assembly.Location,
            package, Path.Combine(own, "result.json"), "0", own], Path.Combine(own, "probe.log"), 30);
        var guardian = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "guardian.json")))!;
        Assert.True(guardian["echild"]!.GetValue<bool>());
        Assert.Equal(0, guardian["driverExitCode"]!.GetValue<int>());
        Assert.Equal(0, guardian["failures"]!.GetValue<int>());
        Assert.Equal(0, guardian["emergencySignals"]!.GetValue<int>());
        Assert.False(guardian["deadline"]!.GetValue<bool>());
        var result = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "result.json")))!;
        Assert.True(result["DirectUnadmittedRefused"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["OriginalStopped"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["ProtectedBytesUnchanged"]!.GetValue<bool>(), result.ToJsonString());
        // Causal assertion follows real setup/refusal and complete original cleanup.
        Assert.True(result["MenuSucceeded"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["ArchiveVerified"]!.GetValue<bool>(), result.ToJsonString());
    }

    /// <summary>Preserves a real committed archive across finalization failure or detected replacement.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualSave_CommittedCloseFailurePreservesDecisionAndReplacement(bool replace)
    {
        FileSystemManager? files = null;
        var closing = 0;
        var sentinel = new IOException("controlled post-Save closing failure");
        var nextGeneration = Guid.NewGuid().ToString("N");
        string? archivePath = null;
        byte[]? archiveBytes = null;
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                SessionOperationClosingAsync = () =>
                {
                    closing++;
                    archivePath = Assert.Single(Directory.GetFiles(files!.ResolvePath("saves/manual_saves"), "*.zip"));
                    archiveBytes = File.ReadAllBytes(archivePath);
                    if (!replace) return Task.FromException(sentinel);
                    var generation = JsonNode.Parse(File.ReadAllBytes(files.SessionGenerationPath))!.AsObject();
                    generation["generationId"] = nextGeneration;
                    File.WriteAllText(files.SessionGenerationPath, generation.ToJsonString());
                    return Task.CompletedTask;
                }
            });
        PortableSaveFixture.Seed(files);
        var originalGeneration = JsonNode.Parse(File.ReadAllBytes(files.SessionGenerationPath))!["generationId"]!.GetValue<string>();
        var input = new QueuedConsoleInputSource(Enumerable.Repeat(Key(ConsoleKey.Enter), 4));
        var runtime = CreateGameEngineRuntime(files, input);
        await using var audio = runtime.AudioService;
        runtime.GameLoop.SetSession("manual-save-close", 43);
        var prior = AnsiConsole.Console;
        using var text = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(text) });
        Exception? failure;
        try { failure = await Record.ExceptionAsync(() => runtime.Engine.InGameOptionsMenu()); }
        finally { AnsiConsole.Console = prior; }
        Assert.Equal(1, closing);
        SaveCreationResult decision;
        if (replace)
        {
            var replacement = Assert.IsType<SessionReplacedException>(failure);
            Assert.Equal(originalGeneration, replacement.ExpectedGeneration);
            decision = Assert.IsType<SaveCreationResult>(replacement.Data["EstablishedOperationResult"]);
            Assert.Contains(CommittedSaveContinuationException.PlayerMessage, text.ToString());
            Assert.Equal(nextGeneration, JsonNode.Parse(File.ReadAllBytes(files.SessionGenerationPath))!["generationId"]!.GetValue<string>());
        }
        else
        {
            var blocked = Assert.IsType<CommittedSaveContinuationException>(failure);
            decision = blocked.Result;
            Assert.True(decision.ContinuationBlocked);
            var retained = Assert.IsType<MainOperationContinuationException<SaveCreationResult>>(decision.Failure);
            Assert.Same(sentinel, retained.InnerException);
        }
        Assert.True(decision.Committed);
        Assert.Equal(archivePath, files.ResolvePath(decision.DestinationRelativePath!));
        Assert.Equal(archiveBytes, File.ReadAllBytes(archivePath!));
        Assert.Single(Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip"));
        Assert.True(input.KeyAvailable); // ordinary success ACK was not consumed
        var loc = new LocalizationManager();
        Assert.DoesNotContain(loc.T("save_success"), text.ToString());
        Assert.DoesNotContain(loc.T("save_failed"), text.ToString());
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out _));
    }

    /// <summary>Runs only beneath the existing native guardian; never borrows a live game world.</summary>
    public static async Task WriteManualSaveActiveMainProbeAsync(string package, string output, int mode, string own)
    {
        Assert.Equal(0, mode);
        var launch = NeutralTerminalLaunch.Create(package, own);
        var root = Directory.GetParent(launch.Scratch)!.FullName;
        var seed = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        PortableSaveFixture.Seed(seed);
        var protectedPaths = new[] { "game_state/meta/soul_state.json", "game_state/world/test_fixture_state.json",
            ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath };
        var before = protectedPaths.ToDictionary(p => p, p => File.ReadAllBytes(seed.ResolvePath(p)));
        var generation = File.ReadAllBytes(seed.SessionGenerationPath);
        using var terminalOutput = new MemoryStream();
        var host = new MainRunCrashScenarioDriver.FixtureHost(launch, terminalOutput);
        using var control = new CancellationTokenSource();
        var server = host.StartServer(control.Token);
        var result = new Dictionary<string, object?>();
        Exception? menuFailure = null;
        var menuSucceeded = false;
        var archiveVerified = false;
        var started = false;
        GmSessionRunIdentity? original = null;
        GmSessionRunCoordinator? originalOwner = null;
        try
        {
            await host.CallAsync("StartShellAsync");
            started = true;
            originalOwner = host.Owner;
            original = originalOwner.Identity;
            var recordPath = Path.Combine(root, ".boe_runtime/gm-runs/main.json");
            var runningBytes = File.ReadAllBytes(recordPath);
            Assert.Equal(GmSessionRunDisposition.Running, GmSessionRunRecordCodec.Decode(runningBytes).Disposition);
            Assert.False(host.Terminal.RootExited.IsCompleted);
            result["OriginalIdentity"] = original;
            Task body;
            using (ExecutionContext.SuppressFlow()) body = Task.Run(async () =>
            {
                Assert.Null(GmSessionRunCoordinator.Current);
                var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
                var runtime = CreateGameEngineRuntime(files,
                    new QueuedConsoleInputSource(Enumerable.Repeat(Key(ConsoleKey.Enter), 4)));
                await using var audio = runtime.AudioService;
                runtime.GameLoop.SetSession("manual-save-active", 43);
                var direct = await runtime.SaveLoad.CreateSaveAsync("unadmitted", "must refuse", turnNumber: 43);
                Assert.Equal(SaveCreationDisposition.Uncertain, direct.Disposition);
                Assert.IsAssignableFrom<IOException>(direct.Failure);
                Assert.Contains("Main run metadata or original owner admission is unavailable", direct.Failure.Message);
                Assert.Empty(Directory.Exists(files.ResolvePath("saves/manual_saves"))
                    ? Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip") : []);
                result["DirectUnadmittedRefused"] = true;
                Assert.Equal(runningBytes, File.ReadAllBytes(recordPath));
                var prior = AnsiConsole.Console;
                using var text = new StringWriter();
                AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
                { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(text) });
                try { menuSucceeded = await runtime.Engine.InGameOptionsMenu(); }
                catch (Exception failure) { menuFailure = failure; }
                finally { AnsiConsole.Console = prior; }
                File.WriteAllText(Path.Combine(own, "menu.txt"), text.ToString());
                Assert.Equal(runningBytes, File.ReadAllBytes(recordPath));
                Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
                var archives = Directory.Exists(files.ResolvePath("saves/manual_saves"))
                    ? Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip") : [];
                result["Archives"] = archives;
                if (menuSucceeded)
                {
                    Assert.Contains(new LocalizationManager().T("save_success"), text.ToString());
                    Assert.DoesNotContain(new LocalizationManager().T("save_failed"), text.ToString());
                    using var zip = ZipFile.OpenRead(Assert.Single(archives));
                    var metadataEntry = Assert.Single(zip.Entries.Where(e => e.FullName == "save_metadata.json"));
                    using var metadataStream = metadataEntry.Open();
                    using var metadata = await JsonDocument.ParseAsync(metadataStream);
                    Assert.Equal(43, metadata.RootElement.GetProperty("turnNumber").GetInt32());
                    Assert.Equal("save_turn43", metadata.RootElement.GetProperty("saveName").GetString());
                    foreach (var path in protectedPaths.Take(2))
                    {
                        using var entry = Assert.Single(zip.Entries.Where(e => e.FullName == path)).Open();
                        using var bytes = new MemoryStream(); await entry.CopyToAsync(bytes);
                        Assert.Equal(before[path], bytes.ToArray());
                    }
                    archiveVerified = true;
                }
            });
            await body.WaitAsync(TimeSpan.FromSeconds(12));
            result["ProtectedBytesUnchanged"] = before.All(p => p.Value.SequenceEqual(File.ReadAllBytes(seed.ResolvePath(p.Key))));
        }
        finally
        {
            if (started) await host.CallAsync("StopShellAsync").WaitAsync(TimeSpan.FromSeconds(5));
            await control.CancelAsync();
            await server.WaitAsync(TimeSpan.FromSeconds(5));
            var stopped = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root)!);
            result["OriginalStopped"] = stopped.Disposition == GmSessionRunDisposition.Stopped &&
                stopped.StopEvidence?.Kind == GmSessionRunStopKind.OwnedScopeEmpty && stopped.Identity == original &&
                originalOwner is { RetainsAuthority: false };
            result["FinalRecord"] = stopped;
            result["MenuSucceeded"] = menuSucceeded;
            result["MenuFailure"] = menuFailure?.ToString();
            result["ArchiveVerified"] = archiveVerified;
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result));
        }
    }
}
