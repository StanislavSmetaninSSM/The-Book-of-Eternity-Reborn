using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Test-only original-process driver. The unchanged PS helper never opens the new
// child bootstrap on RED. A later dedicated production helper uses the same
// actual transport/interpreter with only this FileSystemManager hook injection.
internal static class GmTurnHelperStorageScenario
{
    private const string DataPath = "output/helper-snapshot.json";
    private const string WorldPath = "game_state/world/helper-policy.json";
    private const string SoulPath = "game_state/meta/soul_state.json";
    private const string CompletePath = "ready/turn_complete.json";
    private const string ErrorPath = "ready/turn_error.json";
    private const string AuthorityPath = "game_state/control/pending_turn_snapshot.authority.json";
    private static readonly byte[] A = Encoding.UTF8.GetBytes("{\"value\":\"A\"}");
    private static readonly byte[] B = Encoding.UTF8.GetBytes("{\"value\":\"B\"}");
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static string Journal(FileSystemManager files) => Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
    private static byte[]? Bytes(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

    internal static async Task<int> RunHelperChildAsync(string root, string folder, string expected)
    {
        var report = new Dictionary<string, object?> { ["ExpectedGeneration"] = expected };
        var reads = 0; var recoveries = 0; var contentions = 0;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks {
                MainOwnerLockContendedAsync = () => {
                    Interlocked.Increment(ref contentions);
                    File.WriteAllText(Path.Combine(folder, "helper-owner-contended"), "actual original owner guard");
                    return Task.CompletedTask;
                },
                LocalPublicationRecoveryObserver = (_, _) => Interlocked.Increment(ref recoveries),
                AfterCanonicalWriteLockOpenedAsync = () => { Interlocked.Increment(ref reads); return Task.CompletedTask; }
            });
        try {
            // Exact future production seam is intentionally reflection-only in
            // this unchanged-runtime fixture. No fake result/interpreter exists.
            var type = typeof(FileSystemManager).Assembly.GetType("BookOfEternityClient.Services.GmRuntime.GmTurnHelperControl", throwOnError: true)!;
            var method = type.GetMethod("RunAsync", BindingFlags.Static | BindingFlags.NonPublic,
                null, [typeof(FileSystemManager), typeof(Stream), typeof(Stream), typeof(string)], null)
                ?? throw new MissingMethodException(type.FullName, "RunAsync");
            var task = (Task<int>)method.Invoke(null, [files, Console.OpenStandardInput(), Console.OpenStandardOutput(), expected == "initialize" ? null : expected])!;
            var exit = await task; report["ExitCode"] = exit; return exit;
        } catch (Exception failure) {
            report["Failure"] = failure.ToString(); report["ExitCode"] = 2; return 2;
        } finally {
            report["Completed"] = true; report["CanonicalLeaseOpens"] = reads;
            report["RecoveryEvents"] = recoveries; report["OwnerContentions"] = contentions;
            await File.WriteAllTextAsync(Path.Combine(folder, $"helper-child-{Environment.ProcessId}.json"), JsonSerializer.Serialize(report));
        }
    }

    internal static async Task<int> RunAsync(string mode, string folder)
    {
        var scenario = mode["terminal-main-helper-storage-".Length..];
        var evidence = new Dictionary<string, object?> { ["Mode"] = mode };
        var root = Path.Combine(folder, "session-owner"); Directory.CreateDirectory(root);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        Process? child = null; Task<string>? stdout = null, stderr = null;
        Task? writer = null;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[]? originalGeneration = null;
        var actualCuts = 0;
        try {
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync()) {
                var generation = files.BootstrapLocalStorage(lease, null, JsonSerializer.SerializeToUtf8Bytes(new GameSettings()));
                evidence["InitialGeneration"] = generation;
            }
            originalGeneration = File.ReadAllBytes(files.SessionGenerationPath);
            await SeedAsync(files);
            var selected = scenario switch { "realm-held" => SoulPath, "terminal-held" => ErrorPath, _ => DataPath };
            var before = scenario == "realm-held"
                ? Encoding.UTF8.GetBytes("{\"soulName\":\"Fixture\",\"currentRealm\":\"Chaos Sea\",\"currentIncarnation\":1}")
                : scenario == "terminal-held" ? null : A;
            var after = scenario == "realm-held"
                ? Encoding.UTF8.GetBytes("{\"soulName\":\"Fixture\",\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}")
                : scenario == "terminal-held" ? Encoding.UTF8.GetBytes("{\"sessionId\":\"helper-session\",\"requestId\":\"helper-request\",\"turnNumber\":3,\"status\":\"error\"}") : B;
            if (scenario == "realm-held") await files.WriteFileAtomicBytesAsync(SoulPath, before!);
            if (scenario == "generation-missing") File.Delete(files.SessionGenerationPath);
            if (scenario == "generation-malformed") File.WriteAllText(files.SessionGenerationPath, "{\"schemaVersion\":1,\"generationId\":\"not-current-authority\"}");
            var observedGeneration = Bytes(files.SessionGenerationPath);
            evidence["GenerationAtInvocation"] = observedGeneration;

            async Task StartWriterAsync()
            {
                var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var writingFiles = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                    PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks {
                        LocalPublicationObserver = (phase, index) => {
                            if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
                            using var parsed = JsonDocument.Parse(File.ReadAllBytes(Journal(files)));
                            var members = parsed.RootElement.GetProperty("Members");
                            if (members[index].GetProperty("Path").GetString() != files.ResolvePath(selected)) return;
                            Require(index == 0 && members.GetArrayLength() == 1 && File.ReadAllBytes(files.ResolvePath(selected)).SequenceEqual(after), "Selected publication did not reach its actual distinct after-image.");
                            Interlocked.Increment(ref actualCuts);
                            evidence["HeldJournal"] = File.ReadAllBytes(Journal(files)); evidence["HeldAfter"] = after;
                            reached.TrySetResult(); release.Task.GetAwaiter().GetResult();
                            throw new InvalidOperationException("actual helper read/policy publication cut");
                        }
                    });
                writer = Task.Run(async () => {
                    try { await writingFiles.WriteFileAtomicBytesAsync(selected, after); throw new InvalidOperationException("Selected writer did not fail."); }
                    catch (InvalidOperationException failure) when (failure.Message == "actual helper read/policy publication cut") { evidence["OriginalWriterFailure"] = failure.ToString(); }
                });
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(4));
            }

            if (scenario == "init-held") await StartWriterAsync();
            var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(TestRepoPaths.RepoRoot, "tests/fixtures/GmTurnHelper/storage.ps1"),
                "-RepoRoot", TestRepoPaths.RepoRoot, "-SessionPath", files.GameSessionPath, "-Scenario", scenario, "-Folder", folder,
                "-TestSupport", typeof(GmTurnHelperStorageScenario).Assembly.Location }) start.ArgumentList.Add(arg);
            child = Process.Start(start)!; stdout = child.StandardOutput.ReadToEndAsync(); stderr = child.StandardError.ReadToEndAsync();

            if (scenario is not ("init-held" or "generation-missing" or "generation-malformed")) {
                await WaitForAsync(() => File.Exists(Path.Combine(folder, "initialized")), child, "actual helper initialization");
                if (scenario is "read-held" or "realm-held" or "terminal-held") await StartWriterAsync();
                if (scenario == "stale-load") await ReplaceAndLeaveRecoverableJournalAsync(files, evidence);
                if (scenario == "realm-link") {
                    var foreign = Path.Combine(root, "foreign-realm.json");
                    File.WriteAllBytes(foreign, Encoding.UTF8.GetBytes("{\"currentRealm\":\"Mortal World\"}"));
                    File.Delete(files.ResolvePath(SoulPath)); File.CreateSymbolicLink(files.ResolvePath(SoulPath), foreign);
                    evidence["ActualRealmLink"] = new FileInfo(files.ResolvePath(SoulPath)).LinkTarget;
                }
                File.WriteAllText(Path.Combine(folder, "continue"), "original fixture release");
            }
            if (scenario.EndsWith("held", StringComparison.Ordinal)) {
                await WaitForAsync(() => File.Exists(Path.Combine(folder, "powershell.json")) || File.Exists(Path.Combine(folder, "helper-owner-contended")), child,
                    "old completed helper result or new actual owner-guard contention");
                evidence["HelperCompletedBeforeWriterSettlement"] = File.Exists(Path.Combine(folder, "powershell.json"));
                evidence["ActualHelperOwnerContention"] = File.Exists(Path.Combine(folder, "helper-owner-contended"));
                release.TrySetResult(); await writer!;
                Require(actualCuts == 1 && !File.Exists(Journal(files)), "Selected writer did not settle its real known rollback.");
                Require(before == null ? !File.Exists(files.ResolvePath(selected)) : File.ReadAllBytes(files.ResolvePath(selected)).SequenceEqual(before), "Selected original rollback did not restore exact before bytes/absence.");
                evidence["OriginalKnownRollback"] = true;
            }
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(7));
            evidence["PowerShellExit"] = child.ExitCode; evidence["PowerShellOutput"] = await stdout; evidence["PowerShellError"] = await stderr;
            Require(child.ExitCode == 0, "PowerShell fixture failed before causal assertions: " + await stderr);
            using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "powershell.json")));
            var ps = report.RootElement; evidence["ActualPowerShell"] = ps.Clone();
            evidence["PublicationCuts"] = actualCuts;
            evidence["WorldBytes"] = Bytes(files.ResolvePath(WorldPath)); evidence["AuthorityBytes"] = Bytes(files.ResolvePath(AuthorityPath));
            evidence["GenerationAfterHelper"] = Bytes(files.SessionGenerationPath);
            evidence["JournalAfterHelper"] = Bytes(Journal(files));
            var children = Directory.GetFiles(folder, "helper-child-*.json").Select(path => JsonDocument.Parse(File.ReadAllBytes(path))).ToArray();
            try {
                evidence["ActualHelperChildren"] = children.Select(document => document.RootElement.Clone()).ToArray();
                Require(children.All(document => document.RootElement.GetProperty("Completed").GetBoolean()), "An actual nested helper did not record completion.");
                Require(ps.GetProperty("JoinedHelperTransports").GetInt32() == children.Length, "Nested helper process/IO disposal does not match actual child completions.");
                if (scenario == "stale-load") {
                    Require(children.Sum(document => document.RootElement.GetProperty("RecoveryEvents").GetInt32()) == 0, "Stale helper recovered before rejecting its initialized generation.");
                    Require(File.ReadAllBytes(Journal(files)).SequenceEqual((byte[])evidence["RecoverableJournal"]!), "Stale helper changed pending journal bytes.");
                    Require(File.ReadAllBytes(files.ResolvePath(DataPath)).SequenceEqual(B), "Stale helper changed the recoverable after-image.");
                    evidence["StaleEvidenceRetainedBeforePositiveRecovery"] = true;
                }
            } finally { foreach (var document in children) document.Dispose(); }

            if (scenario is "init-held" or "read-held") {
                Require(ps.GetProperty("Failure").ValueKind == JsonValueKind.Null && ps.GetProperty("Value").GetString() == "A", "Causal RED: helper observed undecided bytes instead of settled A.");
                Require(!(bool)evidence["HelperCompletedBeforeWriterSettlement"]!, "Helper completed while original publication remained undecided.");
            } else if (scenario == "realm-held" || scenario == "realm-link") {
                Require(File.ReadAllBytes(files.ResolvePath(WorldPath)).SequenceEqual(A) && ps.GetProperty("Failure").ValueKind == JsonValueKind.String, "Causal RED: unadmitted realm read permitted an actual forbidden-world write.");
            } else if (scenario == "terminal-held") {
                Require(ps.GetProperty("Failure").ValueKind == JsonValueKind.Null && File.Exists(files.ResolvePath(CompletePath)), "Causal RED: outer completion consumed an undecided terminal signal.");
                using var complete = JsonDocument.Parse(File.ReadAllBytes(files.ResolvePath(CompletePath)));
                Require(complete.RootElement.GetProperty("requestId").GetString() == "helper-request", "Completion lost original request correlation.");
            } else if (scenario == "path-dot") {
                Require(File.ReadAllBytes(files.ResolvePath(AuthorityPath)).SequenceEqual(A) && ps.GetProperty("Failure").ValueKind == JsonValueKind.String, "Causal RED: logical path guard preceded target normalization and wrote protected authority.");
            } else if (scenario == "path-sibling") {
                Require(ps.GetProperty("Failure").ValueKind == JsonValueKind.String && ps.GetProperty("Value").ValueKind == JsonValueKind.Null, "Causal RED: sibling-prefix absolute read escaped the initialized session.");
            } else if (scenario.StartsWith("generation-", StringComparison.Ordinal)) {
                Require(Equal(observedGeneration, Bytes(files.SessionGenerationPath)), "Initialization changed missing/malformed generation authority.");
                Require(ps.GetProperty("Initialized").GetBoolean() == false && ps.GetProperty("Failure").ValueKind == JsonValueKind.String, "Causal RED: initialization accepted missing/malformed generation authority.");
            } else if (scenario == "stale-load") {
                Require(ps.GetProperty("Failure").ValueKind == JsonValueKind.String && ps.GetProperty("Value").ValueKind == JsonValueKind.Null, "Causal RED: stale initialized helper read the replacement generation.");
            }
            evidence["Success"] = true; return 0;
        } catch (Exception failure) { evidence["Failure"] = failure.ToString(); return 1; }
        finally {
            release.TrySetResult();
            try {
                if (writer != null) await writer;
                if (child != null) {
                    if (!child.HasExited) { evidence["ForcedPowerShellTermination"] = true; child.Kill(); await child.WaitForExitAsync(); }
                    if (stdout != null && stderr != null) await Task.WhenAll(stdout, stderr);
                    evidence["OriginalPowerShellExited"] = child.HasExited;
                    child.Dispose();
                }
                // Fixture-owned repairs happen only after the observations above;
                // they neither establish the tested decision nor turn RED green.
                if (scenario.StartsWith("generation-", StringComparison.Ordinal) && originalGeneration != null)
                    File.WriteAllBytes(files.SessionGenerationPath, originalGeneration);
                if (scenario == "realm-link" && File.Exists(files.ResolvePath(SoulPath))) {
                    File.Delete(files.ResolvePath(SoulPath)); File.WriteAllBytes(files.ResolvePath(SoulPath), Encoding.UTF8.GetBytes("{\"currentRealm\":\"Mortal World\"}"));
                }
                var recoveryEvents = 0;
                var closingFiles = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                    new FileSystemManagerHooks { LocalPublicationRecoveryObserver = (_, _) => recoveryEvents++ });
                await using (var lease = await closingFiles.AcquireCanonicalWriteLeaseAsync())
                    Require(closingFiles.ReadExistingSessionGeneration(lease) != null, "Original fixture cannot regain its admitted lease after helper cleanup.");
                evidence["PostObservationRecoveryEvents"] = recoveryEvents;
                if (scenario == "stale-load") Require(recoveryEvents > 0 && File.ReadAllBytes(files.ResolvePath(DataPath)).SequenceEqual(A), "Supposedly recoverable fixture journal never reached actual recovery.");
                evidence["FixtureCanonicalOwnershipReleased"] = true;
            } catch (Exception failure) { evidence["CleanupFailure"] = failure.ToString(); evidence["Success"] = false; }
            await File.WriteAllTextAsync(Path.Combine(folder, "scenario.json"), JsonSerializer.Serialize(evidence));
        }
    }

    private static bool Equal(byte[]? left, byte[]? right) => left == null ? right == null : right != null && left.SequenceEqual(right);
    private static async Task WaitForAsync(Func<bool> observed, Process child, string description)
    {
        var clock = Stopwatch.StartNew();
        while (!observed() && clock.Elapsed < TimeSpan.FromSeconds(6)) {
            if (child.HasExited) throw new InvalidOperationException("PowerShell exited before " + description);
            await Task.Delay(10);
        }
        Require(observed(), "Fixture never reached " + description);
    }

    private static async Task SeedAsync(FileSystemManager files)
    {
        await files.WriteFileAtomicBytesAsync(DataPath, A); await files.WriteFileAtomicBytesAsync(WorldPath, A);
        await files.WriteFileAtomicBytesAsync(AuthorityPath, A);
        await files.WriteFileAtomicAsync(SoulPath, "{\"soulName\":\"Fixture\",\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}");
        await files.WriteFileAtomicAsync("input/turn_request.json", "{\"sessionId\":\"helper-session\",\"requestId\":\"helper-request\",\"turnNumber\":3,\"playerAction\":\"Wait\"}");
        await files.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", "{\"sessionId\":\"helper-session\",\"requestId\":\"helper-request\",\"turnNumber\":3}");
        var sibling = files.GameSessionPath + "-sibling"; Directory.CreateDirectory(sibling); File.WriteAllBytes(Path.Combine(sibling, "outside.json"), B);
    }

    private static async Task ReplaceAndLeaveRecoverableJournalAsync(FileSystemManager files, Dictionary<string, object?> evidence)
    {
        var resources = ResourceBootstrapStateBuilder.BuildPristine();
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.DefinitionsPath, resources.Definitions!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.StatePath, resources.State!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.HistoryPath, resources.History!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, "{\"schemaVersion\":1,\"historicalOwners\":[],\"capacityDrafts\":[]}");
        var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        var logger = new PortableSaveFixture.CaptureLogger(); var saves = new SaveLoadService(files, state, logger);
        Require(await saves.SaveGameAsync("helper-current", "actual initialized-helper replacement"), "Current public save producer refused: " + string.Join("\n", logger.Errors));
        var archive = Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip").Single();
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var loaded = await saves.LoadGameWithOutcomeAsync(archive);
        evidence["ActualLoadDisposition"] = loaded.Disposition.ToString(); evidence["ActualLoadGeneration"] = loaded.EstablishedGeneration;
        Require(loaded.Disposition == LoadReplacementDisposition.Committed && !loaded.NeedsFollowUp && !generation.SequenceEqual(File.ReadAllBytes(files.SessionGenerationPath)), "Real Load did not replace the original initialized generation.");
        await files.WriteFileAtomicBytesAsync(DataPath, A);
        var cuts = 0;
        var faulted = new FileSystemManager(files.BasePath, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks { LocalPublicationObserver = (phase, index) => {
                if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
                using var journal = JsonDocument.Parse(File.ReadAllBytes(Journal(files)));
                Require(index == 0 && journal.RootElement.GetProperty("Members")[index].GetProperty("Path").GetString() == files.ResolvePath(DataPath) && File.ReadAllBytes(files.ResolvePath(DataPath)).SequenceEqual(B), "Replacement journal cut did not reach the original member after-image.");
                cuts++; File.WriteAllBytes(files.ResolvePath(DataPath), [99, 100, 101]);
                throw new InvalidOperationException("actual replacement-generation publication uncertainty");
            } });
        try { await faulted.WriteFileAtomicBytesAsync(DataPath, B); throw new InvalidOperationException("Expected actual unknown was absent."); }
        catch (CoordinatedStatePublicationUncertainException failure) { evidence["ActualNewGenerationUncertainty"] = failure.ToString(); }
        Require(cuts == 1 && File.Exists(Journal(files)), "No actual replacement-generation journal was retained.");
        // Explicit fixture repair to the journal's exact known after-image makes
        // recovery possible without invoking it before the stale helper attempt.
        File.WriteAllBytes(files.ResolvePath(DataPath), B);
        evidence["NewGenerationPublicationCuts"] = cuts; evidence["RecoverableJournal"] = File.ReadAllBytes(Journal(files));
    }
}
