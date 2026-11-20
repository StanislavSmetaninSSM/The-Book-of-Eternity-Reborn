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
    public Task ManualSave_ActiveHostedMainWithoutAmbientGrantPublishesAndCloses() => RunManualSaveHostedProbeAsync(0);

    /// <summary>Actual original admission diagnostics must not contaminate the merged Save result stream.</summary>
    [Fact]
    public Task ManualSave_HealthyAdmissionTraceStaysOutsideMergedConsole() => RunManualSaveHostedProbeAsync(1);

    private async Task<JsonNode> RunHostedSaveLoadProbeAsync(string scenario, string pathPrefix, int mode, int guardianSeconds = 25)
    {
        Assert.True(OperatingSystem.IsLinux());
        var own = Path.Combine("/tmp", pathPrefix + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        _directGachaOutput?.WriteLine("Owned " + scenario + " evidence: " + own);
        var package = Path.Combine(own, "package");
        await RunBrowserColdChildAsync("pwsh", ["-NoLogo", "-NoProfile", "-File",
            Path.Combine(TestRepoPaths.RepoRoot, "scripts/build-linux-supervisor.ps1"),
            "-OutputDirectory", package, "-IncludeHostGuardian", "-IncludeTerminalFixture"],
            Path.Combine(own, "native.log"), 25);
        var support = Path.Combine(Path.GetDirectoryName(typeof(GameEngineTurnLifecycleTests).Assembly.Location)!,
            "BookOfEternityClient.TestSupport.dll");
        await RunBrowserColdChildAsync(Path.Combine(package, "host-guardian"), ["--live-turn",
            Path.Combine(own, "guardian.json"), (guardianSeconds * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet"), support,
            scenario, typeof(GameEngineTurnLifecycleTests).Assembly.Location,
            package, Path.Combine(own, "result.json"), mode.ToString(System.Globalization.CultureInfo.InvariantCulture), own], Path.Combine(own, "probe.log"), guardianSeconds + 5);
        var guardian = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "guardian.json")))!;
        Assert.True(guardian["echild"]!.GetValue<bool>());
        Assert.Equal(0, guardian["driverExitCode"]!.GetValue<int>());
        Assert.Equal(0, guardian["failures"]!.GetValue<int>());
        Assert.Equal(0, guardian["emergencySignals"]!.GetValue<int>());
        Assert.False(guardian["deadline"]!.GetValue<bool>());
        return JsonNode.Parse(File.ReadAllText(Path.Combine(own, "result.json")))!;
    }


    private async Task RunManualSaveHostedProbeAsync(int mode)
    {
        var result = await RunHostedSaveLoadProbeAsync("engine-manual-save-active", "ms-", mode);
        Assert.True(result["DirectUnadmittedRefused"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["OriginalStopped"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["ProtectedBytesUnchanged"]!.GetValue<bool>(), result.ToJsonString());
        // Causal assertion follows real setup/refusal and complete original cleanup.
        Assert.True(result["MenuSucceeded"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["ArchiveVerified"]!.GetValue<bool>(), result.ToJsonString());
        if (mode == 1)
        {
            Assert.True(result["MergedSaveIntervalClean"]!.GetValue<bool>(), result.ToJsonString());
            Assert.True(result["ExternalActiveMarkerJoined"]!.GetValue<bool>(), result.ToJsonString());
        }
    }

    /// <summary>Lists the current Save catalog from Options while real hosted main A stays active.</summary>
    [Fact]
    public async Task ActiveLoadCatalog_OptionsListsSaveAndBackWithoutReplacingOriginal()
    {
        var result = await RunHostedSaveLoadProbeAsync("engine-active-load-catalog", "lc-", 0);
        Assert.True(result["DirectUnadmittedRefused"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["OriginalStopped"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["OriginalUnchangedAtBack"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["NoReplacementOrRequest"]!.GetValue<bool>(), result.ToJsonString());
        // On RED, native setup/ordinary refusal, exact unchanged world, and controlled stop pass.
        // The first success assertion exposes only the actual Options→Load exception/stack.
        Assert.True(result["MenuSucceeded"]!.GetValue<bool>(), result.ToJsonString());
        Assert.Null(result["MenuFailure"]);
        Assert.True(result["ExactSaveAndBackRendered"]!.GetValue<bool>(), result.ToJsonString());
        Assert.True(result["CatalogClosedBeforeInput"]!.GetValue<bool>(), result.ToJsonString());
        Assert.Equal(2, result["LoadMenuReads"]!.GetValue<int>());
    }

    /// <summary>Runs the real Options Load catalog only beneath the bounded neutral native guardian.</summary>
    public static async Task WriteActiveLoadCatalogProbeAsync(string package, string output, int mode, string own)
    {
        Assert.Equal(0, mode);
        var launch = NeutralTerminalLaunch.Create(package, own);
        var root = Directory.GetParent(launch.Scratch)!.FullName;
        var seed = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var state = PortableSaveFixture.Seed(seed);
        var prepare = new SaveLoadService(seed, state, NullLogger<SaveLoadService>.Instance);
        const string saveName = "catalog-active-43";
        Assert.True((await prepare.CreateSaveAsync(saveName, "independent native fixture", turnNumber: 43)).Committed);
        var manual = seed.ResolvePath("saves/manual_saves");
        var archive = Assert.Single(Directory.GetFiles(manual, "*.zip"));
        var archiveBytes = File.ReadAllBytes(archive);
        var generation = File.ReadAllBytes(seed.SessionGenerationPath);
        var canonicalPath = seed.ResolvePath("game_state/world/test_fixture_state.json");
        var canonical = File.ReadAllBytes(canonicalPath);
        using var terminalOutput = new MemoryStream();
        var host = new MainRunCrashScenarioDriver.FixtureHost(launch, terminalOutput);
        using var control = new CancellationTokenSource();
        var server = host.StartServer(control.Token);
        var evidence = new Dictionary<string, object?>();
        GmSessionRunIdentity? original = null;
        GmSessionRunCoordinator? owner = null;
        try
        {
            await host.CallAsync("StartShellAsync");
            owner = host.Owner;
            original = owner.Identity;
            var running = GmSessionRunPersistence.Read(root)!;
            Assert.Equal(original, GmSessionRunRecordCodec.Decode(running).Identity);
            Assert.Equal(GmSessionRunDisposition.Running, GmSessionRunRecordCodec.Decode(running).Disposition);
            Assert.False(host.Terminal.RootExited.IsCompleted);
            Task body;
            using (ExecutionContext.SuppressFlow()) body = Task.Run(async () =>
            {
                Assert.Null(GmSessionRunCoordinator.Current);
                Assert.False(SessionOperationContext.TryGetExpectedGeneration(root, out _));
                var closings = 0;
                var loadStages = new List<string>();
                var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                    PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                    {
                        SessionOperationClosingAsync = () => { closings++; return Task.CompletedTask; },
                        LoadOperationObserver = loadStages.Add
                    });
                var input = new LoadMenuConsoleInput([Key(ConsoleKey.DownArrow), Key(ConsoleKey.Enter)])
                { UnexpectedRead = () => throw new InvalidOperationException("Options invoked unexpected fallback input.") };
                var runtime = CreateGameEngineRuntime(files, input);
                await using var audio = runtime.AudioService;
                runtime.GameLoop.SetSession("active-catalog-43", 43);

                var direct = await Record.ExceptionAsync(() => runtime.SaveLoad.GetAvailableSavesAsync());
                var refusal = Assert.IsAssignableFrom<IOException>(direct);
                Assert.Contains("Main run metadata or original owner admission is unavailable", refusal.Message);
                evidence["DirectUnadmittedRefused"] = true;
                evidence["DirectFailure"] = refusal.ToString();
                Assert.Equal(running, GmSessionRunPersistence.Read(root));

                var closedAtPrompt = true;
                using var console = new LoadMenuAnsiConsole(read =>
                {
                    files.RequireLoadIpcOutsideFileScopes();
                    closedAtPrompt &= closings == 1 && !SessionOperationContext.TryGetExpectedGeneration(root, out _)
                        && GmSessionRunPersistence.Read(root)!.AsSpan().SequenceEqual(running);
                    return read switch
                    {
                        0 => Key(ConsoleKey.DownArrow),
                        1 => Key(ConsoleKey.Enter),
                        _ => throw new InvalidOperationException("Catalog read a key beyond Back.")
                    };
                });
                var previousConsole = AnsiConsole.Console;
                var menuSucceeded = false;
                Exception? menuFailure;
                AnsiConsole.Console = console;
                try { menuFailure = await Record.ExceptionAsync(async () => { menuSucceeded = await runtime.Engine.InGameOptionsMenu(); }); }
                finally { AnsiConsole.Console = previousConsole; }

                input.AssertCompleted();
                Assert.Equal(2, input.Reads);
                var rendered = console.Output;
                File.WriteAllText(Path.Combine(own, "load-catalog-menu.txt"), rendered);
                evidence["MenuFailure"] = menuFailure?.ToString();
                evidence["MenuSucceeded"] = menuSucceeded;
                evidence["LoadMenuReads"] = console.Reads;
                evidence["CatalogClosedBeforeInput"] = console.Reads > 0 && closedAtPrompt && closings == 1;
                evidence["ClosingCount"] = closings;
                evidence["ExactSaveAndBackRendered"] = rendered.Contains("Выберите сохранение:", StringComparison.Ordinal)
                    && rendered.Contains(saveName + " | Ход 43 |", StringComparison.Ordinal)
                    && rendered.Contains(new LocalizationManager { CurrentLanguage = "ru" }.T("back"), StringComparison.Ordinal);
                var afterMenu = GmSessionRunPersistence.Read(root)!;
                evidence["OriginalUnchangedAtBack"] = original == GmSessionRunRecordCodec.Decode(afterMenu).Identity
                    && afterMenu.AsSpan().SequenceEqual(running) && !host.Terminal.RootExited.IsCompleted
                    && File.ReadAllBytes(files.SessionGenerationPath).AsSpan().SequenceEqual(generation)
                    && Assert.Single(Directory.GetFiles(manual, "*.zip")) == archive
                    && File.ReadAllBytes(archive).AsSpan().SequenceEqual(archiveBytes)
                    && File.ReadAllBytes(canonicalPath).AsSpan().SequenceEqual(canonical);
                evidence["NoReplacementOrRequest"] = loadStages.Count == 0 && new[]
                    {
                        "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
                        "game_state/control/pending_turn_snapshot.authority.json", "ready/turn_complete.json", "ready/turn_error.json"
                    }.All(path => !File.Exists(files.ResolvePath(path)))
                    && !Directory.Exists(files.ResolvePath("game_state/control/pending_turn_snapshot"))
                    && runtime.GameLoop.SessionId == "active-catalog-43" && runtime.GameLoop.TurnNumber == 43;
                evidence["LoadOperationStages"] = loadStages;
            });
            await body.WaitAsync(TimeSpan.FromSeconds(12));
        }
        finally
        {
            if (owner != null) await host.CallAsync("StopShellAsync").WaitAsync(TimeSpan.FromSeconds(5));
            await control.CancelAsync();
            await server.WaitAsync(TimeSpan.FromSeconds(5));
            var stopped = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root)!);
            evidence["OriginalStopped"] = stopped.Disposition == GmSessionRunDisposition.Stopped
                && stopped.StopEvidence?.Kind == GmSessionRunStopKind.OwnedScopeEmpty && stopped.Identity == original
                && owner is { RetainsAuthority: false };
            evidence["FinalRecord"] = stopped;
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(evidence));
        }
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
        var input = new ManualSaveCountingInput();
        var readsAtClosing = -1;
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                SessionOperationClosingAsync = () =>
                {
                    closing++;
                    archivePath = Assert.Single(Directory.GetFiles(files!.ResolvePath("saves/manual_saves"), "*.zip"));
                    archiveBytes = File.ReadAllBytes(archivePath);
                    readsAtClosing = input.Reads; // all prompt/paste reads are already complete
                    if (!replace) return Task.FromException(sentinel);
                    var generation = JsonNode.Parse(File.ReadAllBytes(files.SessionGenerationPath))!.AsObject();
                    generation["generationId"] = nextGeneration;
                    File.WriteAllText(files.SessionGenerationPath, generation.ToJsonString());
                    return Task.CompletedTask;
                }
            });
        PortableSaveFixture.Seed(files);
        var originalGeneration = JsonNode.Parse(File.ReadAllBytes(files.SessionGenerationPath))!["generationId"]!.GetValue<string>();
        var runtime = CreateGameEngineRuntime(files, input);
        await using var audio = runtime.AudioService;
        runtime.GameLoop.SetSession("manual-save-close", 43);
        var prior = AnsiConsole.Console;
        using var text = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(text) });
        AnsiConsole.Console.Profile.Width = 240; // deterministic presentation, not terminal/controller geometry
        Exception? failure;
        try { failure = await Record.ExceptionAsync(() => runtime.Engine.InGameOptionsMenu()); }
        finally { AnsiConsole.Console = prior; }
        _directGachaOutput?.WriteLine("Manual Save close rendered output (replace=" + replace + "):\n" + text);
        Assert.Equal(1, closing);
        SaveCreationResult decision;
        if (replace)
        {
            var replacement = Assert.IsType<SessionReplacedException>(failure);
            Assert.Equal(originalGeneration, replacement.ExpectedGeneration);
            decision = Assert.IsType<SaveCreationResult>(replacement.Data["EstablishedOperationResult"]);
            Assert.True(text.ToString().Contains(CommittedSaveContinuationException.PlayerMessage, StringComparison.Ordinal), text.ToString());
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
        Assert.True(readsAtClosing >= 1);
        Assert.Equal(readsAtClosing, input.Reads); // no ReadKey, including fallback, after actual closing
        var loc = new LocalizationManager();
        Assert.DoesNotContain(loc.T("save_success"), text.ToString());
        Assert.DoesNotContain(loc.T("save_failed"), text.ToString());
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out _));
    }

    private sealed class ManualSaveCountingInput : IConsoleInputSource
    {
        private readonly QueuedConsoleInputSource _inner = new(Enumerable.Repeat(Key(ConsoleKey.Enter), 4));
        internal int Reads { get; private set; }
        public bool IsScripted => _inner.IsScripted;
        public bool KeyAvailable => _inner.KeyAvailable;
        public ConsoleKeyInfo ReadKey(bool intercept = true) { Reads++; return _inner.ReadKey(intercept); }
        public string? ReadLine() => _inner.ReadLine();
        public void AssertCompleted() => _inner.AssertCompleted();
    }

    /// <summary>Runs only beneath the existing native guardian; never borrows a live game world.</summary>
    public static async Task WriteManualSaveActiveMainProbeAsync(string package, string output, int mode, string own)
    {
        Assert.InRange(mode, 0, 1);
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
        var trace = Path.Combine(own, "external-trace");
        var nonce = Guid.NewGuid().ToString("N");
        var oldRoute = Environment.GetEnvironmentVariable(MainOperationFailureDiagnostic.PhysicalRouteVariable);
        var oldPath = Environment.GetEnvironmentVariable(MainOperationFailureDiagnostic.PhysicalDirectoryVariable);
        var oldNonce = Environment.GetEnvironmentVariable(GmSessionRunCoordinator.StopDiagnosticNonceVariable);
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
            if (mode == 1)
            {
                Directory.CreateDirectory(trace, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                var marker = Path.Combine(trace, "owner-nonce");
                File.WriteAllText(marker, nonce, new System.Text.UTF8Encoding(false));
                File.SetUnixFileMode(marker, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                Environment.SetEnvironmentVariable(MainOperationFailureDiagnostic.PhysicalRouteVariable, "external");
                Environment.SetEnvironmentVariable(MainOperationFailureDiagnostic.PhysicalDirectoryVariable, trace);
                Environment.SetEnvironmentVariable(GmSessionRunCoordinator.StopDiagnosticNonceVariable, nonce);
            }
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
                var originalError = Console.Error;
                if (mode == 1) Console.SetError(text); // isolated child only; same writer, not a PTY claim
                try { menuSucceeded = await runtime.Engine.InGameOptionsMenu(); }
                catch (Exception failure) { menuFailure = failure; }
                finally { Console.SetError(originalError); AnsiConsole.Console = prior; }
                if (mode == 1)
                {
                    const string description = "Описание (необязательно): ";
                    var rendered = text.ToString();
                    var boundary = rendered.LastIndexOf(description, StringComparison.Ordinal);
                    result["MergedSaveIntervalClean"] = boundary >= 0 &&
                        rendered[(boundary + description.Length)..] == new LocalizationManager().T("save_success") + Environment.NewLine;
                    var activeMarkers = new List<JsonElement>();
                    foreach (var path in Directory.GetFiles(trace, "*.json"))
                    {
                        var raw = File.ReadAllText(path);
                        const string prefix = "BOE_MAIN_OPERATION_MARKER ";
                        if (!raw.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            Assert.StartsWith("BOE_CANONICAL_LEASE_OBSERVATION ", raw);
                            continue; // Physical rows have their separate strict lifecycle regression.
                        }
                        Assert.EndsWith(Environment.NewLine, raw);
                        using var row = JsonDocument.Parse(raw[prefix.Length..]);
                        var value = row.RootElement;
                        Assert.Equal(files.BasePath, value.GetProperty("Root").GetString());
                        Assert.Equal(nonce, value.GetProperty("Nonce").GetString());
                        Assert.Equal(Environment.ProcessId, value.GetProperty("ProcessId").GetInt32());
                        Assert.True(value.GetProperty("Occurrence").GetInt64() > 0);
                        Assert.True(value.GetProperty("CaptureMonotonicTimestamp").GetInt64() > 0);
                        Assert.Equal($"{Environment.ProcessId}-marker-{value.GetProperty("Occurrence").GetInt64()}-{value.GetProperty("Stage").GetString()}.json", Path.GetFileName(path));
                        Assert.Equal("active-received", value.GetProperty("Stage").GetString());
                        Assert.Equal(JsonValueKind.Null, value.GetProperty("OriginalClose").ValueKind);
                        activeMarkers.Add(value.Clone());
                    }
                    var joined = false;
                    if (activeMarkers.Count == 1)
                    {
                        var row = activeMarkers[0];
                        var pin = JsonSerializer.Deserialize<MainOperationReply>(row.GetProperty("OriginalPin"), MainOperationReader.Json)!;
                        joined = row.GetProperty("Nonce").GetString() == nonce && row.GetProperty("ProcessId").GetInt32() == Environment.ProcessId &&
                            pin.Ok && pin.State == MainOperationState.Active && pin.Identity == original &&
                            Guid.TryParseExact(pin.PinId, "N", out _) && Guid.TryParseExact(pin.CloseId, "N", out _) && Guid.TryParseExact(pin.OperationId, "N", out _);
                    }
                    result["ExternalActiveMarkerJoined"] = joined;
                }
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
            Environment.SetEnvironmentVariable(MainOperationFailureDiagnostic.PhysicalRouteVariable, oldRoute);
            Environment.SetEnvironmentVariable(MainOperationFailureDiagnostic.PhysicalDirectoryVariable, oldPath);
            Environment.SetEnvironmentVariable(GmSessionRunCoordinator.StopDiagnosticNonceVariable, oldNonce);
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
