using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Local #1557 fixture only. No provider, process supervisor, source override or storage mock.
internal static partial class GmDocumentationDeliveryScenario
{
    internal sealed record Doc(string Path, string Role);
    // Independent approved inventory: not obtained from daemon docSpecs or its resulting manifest.
    internal static readonly Doc[] Expected =
    [
        new("CLI_Agent_Daemon_Specification.md", "daemon_protocol"),
        new("TaskGuides/CLI_Step_Main.txt", "main_turn_guide"),
        new("Examples/E_CLI_Step_Main.txt", "main_turn_example"),
        new("Examples/E_CLI_Mortal_Item_Materialization.txt", "mortal_item_materialization_examples"),
        new("Examples/E_CLI_Mortal_Resources.txt", "mortal_resource_materialization_examples"),
        new("OtherGuides/Effect_Materialization_Contract.md", "effect_materialization_contract"),
        new("Examples/E_CLI_Effect_Materialization.txt", "effect_materialization_examples"),
        new("OtherGuides/Wound_Materialization_Contract.md", "wound_materialization_contract"),
        new("Examples/E_CLI_Wound_Materialization.txt", "wound_treatment_scene_authority_examples"),
        new("Examples/E_CLI_Afterlife_Turns.txt", "afterlife_turn_examples"),
        new("Examples/E_CLI_Training_Showcases.txt", "training_showcase_example"),
        new("OtherGuides/Afterlife_Contract_Matrix.md", "afterlife_contract_matrix"),
        new("OtherGuides/Afterlife_Combat_Terminology_Glossary.md", "afterlife_combat_glossary"),
        new("Examples/E_CLI_Ink_Feather_Actions.txt", "ink_feather_action_example"),
        new("CLI_Rules_Index.md", "gm_situation_router"), new("OtherGuides/GM_Common_Core.md", "gm_common_core"),
        new("OtherGuides/Afterlife_Lifecycle.md", "afterlife_rules_lifecycle"),
        new("OtherGuides/Afterlife_Scheduler.md", "afterlife_rules_scheduler"),
        new("OtherGuides/Afterlife_Guardian_Contracts.md", "afterlife_rules_guardian_contracts"),
        new("OtherGuides/Afterlife_Abodes_Residents.md", "afterlife_rules_abodes_residents"),
        new("OtherGuides/Afterlife_Shining_Actions.md", "afterlife_rules_shining_actions"),
        new("OtherGuides/Afterlife_Spiritual_Conflict.md", "afterlife_rules_spiritual_conflict"),
        new("OtherGuides/Afterlife_Effects_Resources.md", "afterlife_rules_effects_resources"),
        new("OtherGuides/Afterlife_Actor_Profiles.md", "afterlife_rules_actor_profiles"),
        new("OtherGuides/Afterlife_Story_Memory.md", "afterlife_rules_story_memory"),
        new("OtherGuides/Afterlife_Wound_Continuation.md", "afterlife_rules_wound_continuation"),
        new("Examples/E_CLI_Afterlife_Lifecycle.txt", "afterlife_examples_lifecycle"),
        new("Examples/E_CLI_Afterlife_Scheduler.txt", "afterlife_examples_scheduler"),
        new("Examples/E_CLI_Afterlife_Guardian_Contracts.txt", "afterlife_examples_guardian_contracts"),
        new("Examples/E_CLI_Afterlife_Abodes_Residents.txt", "afterlife_examples_abodes_residents"),
        new("Examples/E_CLI_Afterlife_Shining_Actions.txt", "afterlife_examples_shining_actions"),
        new("Examples/E_CLI_Afterlife_Spiritual_Conflict.txt", "afterlife_examples_spiritual_conflict"),
        new("Examples/E_CLI_Afterlife_Effects_Resources.txt", "afterlife_examples_effects_resources"),
        new("Examples/E_CLI_Afterlife_Actor_Profiles.txt", "afterlife_examples_actor_profiles"),
        new("Examples/E_CLI_Afterlife_Story_Memory.txt", "afterlife_examples_story_memory"),
        new("Examples/E_CLI_Afterlife_Wound_Continuation.txt", "afterlife_examples_wound_continuation")
    ];
    internal sealed record Identity(int Pid, long BirthTicks, int ParentPid, string[] Args, string Role, string State);
    private sealed record ProcessStat(int Pid, long BirthTicks, int ParentPid, string State);
    private sealed class OwnershipEvidence
    {
        internal readonly JsonArray Observations = new();
        internal bool Unresolved; // Sticky: later terminal/EOF evidence cannot clear a prior conflict.
    }
    internal sealed record Result(JsonObject Evidence, JsonObject? Ast, string? Failure, IReadOnlyList<string> Issues);
    private sealed class Clock
    {
        internal readonly Stopwatch Watch = Stopwatch.StartNew();
        private double _cleanupEnd = 60;
        private bool _cleanupStarted;
        internal TimeSpan Work => Remaining(45);
        internal TimeSpan Cleanup => Remaining(_cleanupEnd);
        internal TimeSpan Total => Remaining(60);
        internal TimeSpan Operation => _cleanupStarted ? Cleanup : Work;
        internal void BeginCleanup() { _cleanupStarted = true; _cleanupEnd = Math.Min(60, Watch.Elapsed.TotalSeconds + 15); }
        private TimeSpan Remaining(double end)
        {
            var value = end - Watch.Elapsed.TotalSeconds;
            if (value <= 0) throw new TimeoutException("delivery.absolute_deadline");
            return TimeSpan.FromSeconds(value);
        }
    }
    private sealed class PipeLog
    {
        private readonly StringBuilder _text = new();
        internal bool Eof; internal string? Error;
        internal string Text { get { lock (_text) return _text.ToString(); } }
        internal async Task PumpAsync(StreamReader reader, Clock clock, CancellationToken token)
        {
            try
            {
                var buffer = new char[4096];
                while (true)
                {
                    var length = await reader.ReadAsync(buffer.AsMemory(), token).AsTask().WaitAsync(clock.Total, token);
                    if (length == 0) { Eof = true; return; }
                    lock (_text)
                    {
                        if (_text.Length + length > 8 * 1024 * 1024) throw new InvalidDataException("delivery.pipe_overflow");
                        _text.Append(buffer, 0, length);
                    }
                }
            }
            catch (Exception error) { Error = error.ToString(); }
        }
    }
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static async Task<Result> RunAsync(string repo, bool publish, Func<byte[], byte[]>? sourceMutation = null,
        Func<byte[], byte[]>? sourceSeedMutation = null, string? physicalFault = null)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This exact delivery responsibility is Linux-only.");
        if (physicalFault is not null && (!publish || physicalFault is not ("missing_package_source" or "missing_session_copy")))
            throw new ArgumentException("Physical delivery faults require the unchanged real publishing producer.", nameof(physicalFault));
        var clock = new Clock();
        var id = Guid.NewGuid().ToString("N");
        var package = Path.Combine(Path.GetTempPath(), "boe-gm1557-package-" + id);
        var root = Path.Combine(Path.GetTempPath(), "boe-gm1557-session-" + id);
        var artifacts = Path.Combine(Path.GetTempPath(), "boe-gm1557-evidence-" + id);
        var evidence = new JsonObject { ["package"] = package, ["root"] = root, ["artifacts"] = artifacts,
            ["workLimitSeconds"] = 45, ["cleanupLimitSeconds"] = 15, ["totalLimitSeconds"] = 60, ["publish"] = publish };
        var issues = new List<string>(); var cleanupErrors = new List<string>();
        var authorized = new Dictionary<(int, long), Identity>();
        var unknown = new Dictionary<(int, long), Identity>();
        var ownership = new OwnershipEvidence();
        var reportPath = Path.Combine(artifacts, "ast-and-publication.json");
        var initialIdentityProved = false; var pipesStarted = false; var initialCensusProved = false;
        Process? child = null; Identity? original = null; JsonObject? ast = null;
        var output = new PipeLog(); var errorOutput = new PipeLog(); Task? outputPump = null, errorPump = null;
        using var pipeCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        string? failure = null; var exitConfirmed = false; var rootsRemoved = false;
        try
        {
            Directory.CreateDirectory(artifacts);
            var config = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
            var built = Path.Combine(repo, "BookOfEternityClient", "bin", config, "net8.0");
            var builtDll = Path.Combine(built, "BookOfEternityClient.dll");
            if (Hash(File.ReadAllBytes(builtDll)) != Hash(File.ReadAllBytes(typeof(GameEngine).Assembly.Location)))
                throw new InvalidDataException("delivery.stale_package_dll");
            CopyTree(built, package, clock);
            var daemon = Path.Combine(package, "game_master_daemon.ps1");
            var source = File.ReadAllBytes(Path.Combine(repo, "BookOfEternityClient/game_master_daemon.ps1"));
            if (!File.ReadAllBytes(daemon).SequenceEqual(source)) throw new InvalidDataException("delivery.stale_packaged_daemon");
            var required = new[] { "BookOfEternityClient.dll", "BookOfEternityClient.deps.json", "BookOfEternityClient.runtimeconfig.json",
                "Launcher/gm_main_operation.ps1", "Launcher/GM_Turn_Helper.ps1", "operational-resources/CLI_Agent_Daemon_Specification.md" };
            var pins = new JsonObject();
            foreach (var path in required)
            {
                _ = clock.Work;
                var full = Path.Combine(package, path);
                if (!File.Exists(full)) throw new InvalidDataException("delivery.package_prerequisite:" + path);
                pins[path] = Hash(File.ReadAllBytes(full));
            }
            if (physicalFault == "missing_package_source")
            {
                const string relative = "OtherGuides/GM_Common_Core.md";
                var physical = Path.Combine(package, "operational-resources", relative);
                var original = File.ReadAllBytes(physical);
                if (!original.SequenceEqual(File.ReadAllBytes(Path.Combine(repo, relative))))
                    throw new InvalidDataException("delivery.physical_source_fault_seed_mismatch");
                File.Delete(physical);
                evidence["physicalFault"] = new JsonObject { ["kind"] = physicalFault, ["relativePath"] = relative,
                    ["physicalPath"] = physical, ["beforeSha256"] = Hash(original), ["presentBefore"] = true,
                    ["presentAfter"] = File.Exists(physical), ["phase"] = "before unchanged real producer" };
            }
            if (publish && (sourceMutation is not null || sourceSeedMutation is not null)) throw new InvalidDataException("Only nonexecuting AST fixtures may mutate source.");
            if (sourceMutation is not null) File.WriteAllBytes(daemon, sourceMutation(source));
            evidence["sourceDaemonHash"] = Hash(source); evidence["packagedDaemonHash"] = Hash(File.ReadAllBytes(daemon));
            evidence["packagePins"] = pins; evidence["assemblyIdentity"] = System.Reflection.AssemblyName.GetAssemblyName(builtDll).FullName;
            var packageInventory = new JsonObject();
            foreach (var path in Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories))
            {
                _ = clock.Work;
                packageInventory[Path.GetRelativePath(package, path).Replace('\\', '/')] = Hash(File.ReadAllBytes(path));
            }
            evidence["packageInventory"] = packageInventory;
            evidence["nonexecutingSourceMutation"] = sourceMutation is not null;
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            files.EnsureDirectoryStructure();
            if (Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories).Any())
                throw new InvalidDataException("delivery.fixture_namespace_not_empty");
            evidence["initialSessionFiles"] = new JsonArray();
            var driver = Path.Combine(artifacts, "inspect-and-publish.ps1");
            File.Copy(Path.Combine(repo, "tests/fixtures/GmDocumentationDelivery/inspect-and-publish.ps1"), driver);
            evidence["driverHash"] = Hash(File.ReadAllBytes(driver));
            string? seedPath = null;
            if (sourceSeedMutation is not null)
            {
                seedPath = Path.Combine(artifacts, "nonexecuting-source-seed.ps1");
                File.WriteAllBytes(seedPath, sourceSeedMutation(source));
            }
            var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, WorkingDirectory = package };
            foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", driver, "-DaemonPath", daemon,
                         "-SessionPath", files.GameSessionPath, "-ReportPath", reportPath, "-Mode", publish ? "publish" : "inspect" })
                start.ArgumentList.Add(arg);
            if (seedPath is not null) { start.ArgumentList.Add("-SeedDaemonPath"); start.ArgumentList.Add(seedPath); }
            child = Process.Start(start) ?? throw new InvalidOperationException("delivery.pwsh_not_started");
            // Start both retained drains even if the first ownership read subsequently fails.
            outputPump = output.PumpAsync(child.StandardOutput, clock, pipeCancellation.Token);
            errorPump = errorOutput.PumpAsync(child.StandardError, clock, pipeCancellation.Token);
            pipesStarted = true;
            var candidate = ReadIdentity(child.Id, "pwsh", ownership.Observations, start.ArgumentList.ToArray());
            if (candidate.Args.Length == 0 || Path.GetFileName(candidate.Args[0]) != "pwsh" ||
                !candidate.Args.Skip(1).SequenceEqual(start.ArgumentList)) throw new InvalidDataException("delivery.pwsh_identity_mismatch");
            original = candidate;
            authorized.Add((original.Pid, original.BirthTicks), original);
            initialIdentityProved = true;
            Census(package, root, authorized, unknown, issues, ownership, child, clock);
            initialCensusProved = true;
            while (!child.HasExited)
            {
                Census(package, root, authorized, unknown, issues, ownership, child, clock);
                if (output.Error is not null || errorOutput.Error is not null) throw new InvalidDataException("delivery.pipe_failure");
                await Task.Delay(TimeSpan.FromMilliseconds(20)).WaitAsync(clock.Work);
            }
            await child.WaitForExitAsync().WaitAsync(clock.Work);
            await Task.WhenAll(outputPump, errorPump).WaitAsync(clock.Work);
            evidence["exitCode"] = child.ExitCode;
            if (!output.Eof || !errorOutput.Eof || output.Error is not null || errorOutput.Error is not null)
                throw new InvalidDataException("delivery.pipe_eof_unproved");
            if (File.Exists(reportPath)) ast = JsonNode.Parse(File.ReadAllBytes(reportPath))!.AsObject();
            evidence["ast"] = ast?.DeepClone();
            if (child.ExitCode != 0 || ast?["completed"]?.GetValue<bool>() != true)
                throw new InvalidDataException("delivery.adapter_failure:" + ast?["failure"]?.GetValue<string>());
            if (publish)
            {
                if (authorized.Values.Count(identity => identity.Role == "helper") < 2) issues.Add("delivery.helper_scope_census_incomplete");
                var manifestPath = Path.Combine(files.GameSessionPath, "game_state/control/gm_context_pack/context_pack_manifest.json");
                var manifest = JsonNode.Parse(File.ReadAllBytes(manifestPath))!.AsObject();
                evidence["manifest"] = manifest.DeepClone();
                if (physicalFault == "missing_session_copy")
                {
                    const string relative = "OtherGuides/GM_Common_Core.md";
                    var target = Path.Combine(files.GameSessionPath, "game_state/control/gm_context_pack", relative);
                    var sourcePath = Path.Combine(package, "operational-resources", relative);
                    var bytes = File.ReadAllBytes(target);
                    if (!bytes.SequenceEqual(File.ReadAllBytes(sourcePath)) ||
                        manifest["docs"]!.AsArray().Count(row => row?["relativePath"]?.GetValue<string>() == relative) != 1)
                        throw new InvalidDataException("delivery.physical_copy_fault_seed_mismatch");
                    File.Delete(target);
                    evidence["physicalFault"] = new JsonObject { ["kind"] = physicalFault, ["relativePath"] = relative,
                        ["physicalPath"] = target, ["beforeSha256"] = Hash(bytes), ["presentBefore"] = true,
                        ["presentAfter"] = File.Exists(target), ["phase"] = "after unchanged real producer, before closure check" };
                }
                var docs = CollectDelivered(files.GameSessionPath, manifest, package, repo, issues, clock);
                evidence["copiedPins"] = new JsonObject(docs.Select(pair => KeyValuePair.Create<string, JsonNode?>(pair.Key, JsonValue.Create(Hash(pair.Value)))));
                issues.AddRange(GmDocumentationDeliveryRules.VerifyClosure(docs, files.GameSessionPath + "/game_state/control/gm_context_pack"));
                foreach (var path in new[] { "input/turn_request.json", "input/qte_effect_resolution_request.json", "ready/turn_complete.json",
                             "game_state/control/validation_repair_request.json", "game_state/control/terminal_protocol_failure_request.json",
                             "game_state/control/pending_turn_snapshot.json" })
                    if (File.Exists(Path.Combine(files.GameSessionPath, path))) issues.Add("delivery.unexpected_dispatch:" + path);
            }
        }
        catch (Exception error) { failure = error.ToString(); }
        finally
        {
            evidence["workElapsedSeconds"] = clock.Watch.Elapsed.TotalSeconds;
            clock.BeginCleanup();
            try
            {
                if (original is not null) Census(package, root, authorized, unknown, issues, ownership, child, clock);
                foreach (var identity in authorized.Values.OrderBy(identity => identity.Role == "helper" ? 0 : 1).ToArray())
                {
                    _ = clock.Cleanup;
                    if (!ObserveOwned(identity, identity.Role == "pwsh" ? child : null, ownership, clock, "cleanup_before_handle")) continue;
                    using var process = Process.GetProcessById(identity.Pid);
                    // Only a fresh exact live match may authorize this individual signal.
                    if (!ObserveOwned(identity, identity.Role == "pwsh" ? child : null, ownership, clock, "cleanup_before_signal")) continue;
                    process.Kill(); // Exact retained identity only; never delegate descendant selection.
                    await process.WaitForExitAsync().WaitAsync(clock.Cleanup);
                }
                if (child is not null) await child.WaitForExitAsync().WaitAsync(clock.Cleanup);
                if (outputPump is not null && errorPump is not null) await Task.WhenAll(outputPump, errorPump).WaitAsync(clock.Cleanup);
                if (original is not null) Census(package, root, authorized, unknown, issues, ownership, child, clock);
                var remaining = authorized.Values.Where(identity => ObserveOwned(identity, identity.Role == "pwsh" ? child : null,
                    ownership, clock, "final_exit_check")).ToArray();
                evidence["remaining"] = System.Text.Json.JsonSerializer.SerializeToNode(remaining);
                exitConfirmed = initialIdentityProved && pipesStarted && initialCensusProved && unknown.Count == 0 && !ownership.Unresolved &&
                    remaining.Length == 0 && child is not null && child.HasExited &&
                    outputPump is not null && errorPump is not null && output.Eof && errorOutput.Eof &&
                    output.Error is null && errorOutput.Error is null;
                if (!exitConfirmed) throw new InvalidDataException("delivery.cleanup_exit_or_eof_unproved");
                foreach (var path in new[] { package, root })
                {
                    _ = clock.Cleanup;
                    if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                    if (Directory.Exists(path)) throw new IOException("delivery.root_removal_unproved:" + path);
                }
                rootsRemoved = true;
            }
            catch (Exception error) { cleanupErrors.Add(error.ToString()); }
            finally
            {
                pipeCancellation.Cancel();
                // Diagnostic artifacts are independent of ownership qualification; never erase the original failure.
                try
                {
                    if (child is not null && child.HasExited) evidence["exitCode"] = child.ExitCode;
                    if (File.Exists(reportPath)) ast = JsonNode.Parse(File.ReadAllBytes(reportPath))!.AsObject();
                    evidence["ast"] = ast?.DeepClone();
                }
                catch (Exception error) { evidence["artifactRetentionFailure"] = error.ToString(); }
                if (child is not null) { child.StandardOutput.Dispose(); child.StandardError.Dispose(); child.Dispose(); }
                evidence["stdout"] = output.Text; evidence["stderr"] = errorOutput.Text;
                evidence["stdoutEof"] = output.Eof; evidence["stderrEof"] = errorOutput.Eof;
                evidence["stdoutFailure"] = output.Error; evidence["stderrFailure"] = errorOutput.Error;
                evidence["identities"] = System.Text.Json.JsonSerializer.SerializeToNode(authorized.Values.ToArray());
                evidence["unknownDescendants"] = System.Text.Json.JsonSerializer.SerializeToNode(unknown.Values.ToArray());
                evidence["ownershipObservations"] = ownership.Observations;
                evidence["ownershipUnresolved"] = ownership.Unresolved;
                evidence["initialIdentityProved"] = initialIdentityProved;
                evidence["pipesStarted"] = pipesStarted; evidence["initialCensusProved"] = initialCensusProved;
                evidence["exitConfirmed"] = exitConfirmed; evidence["rootsRemoved"] = rootsRemoved;
                evidence["elapsedSeconds"] = clock.Watch.Elapsed.TotalSeconds;
                if (clock.Watch.Elapsed.TotalSeconds > 60) cleanupErrors.Add("delivery.absolute_deadline_exceeded");
                evidence["failure"] = failure; evidence["cleanupFailures"] = System.Text.Json.JsonSerializer.SerializeToNode(cleanupErrors);
                evidence["issues"] = System.Text.Json.JsonSerializer.SerializeToNode(issues);
                if (Directory.Exists(artifacts))
                {
                    File.WriteAllText(Path.Combine(artifacts, "stdout.log"), output.Text);
                    File.WriteAllText(Path.Combine(artifacts, "stderr.log"), errorOutput.Text);
                    File.WriteAllText(Path.Combine(artifacts, "outcome.json"), evidence.ToJsonString());
                }
            }
        }
        if (cleanupErrors.Count != 0) issues.Add("delivery.cleanup_failed");
        return new(evidence, ast, failure, issues);
    }
    private static void CopyTree(string source, string destination, Clock clock)
    {
        Directory.CreateDirectory(destination);
        foreach (var path in Directory.EnumerateFileSystemEntries(source))
        {
            _ = clock.Work;
            if (new FileInfo(path).LinkTarget is not null) throw new InvalidDataException("delivery.package_symlink:" + path);
            var target = Path.Combine(destination, Path.GetFileName(path));
            if (Directory.Exists(path)) CopyTree(path, target, clock); else File.Copy(path, target);
        }
    }
    private static Dictionary<string, byte[]> CollectDelivered(string session, JsonObject manifest, string package,
        string repo, List<string> issues, Clock clock)
    {
        var pack = Path.Combine(session, "game_state/control/gm_context_pack");
        var resources = Path.Combine(package, "operational-resources");
        if (manifest["repoRootPath"]?.GetValue<string>() != resources) issues.Add("delivery.checkout_fallback");
        var rows = manifest["docs"]!.AsArray().Select(row => row!.AsObject()).ToArray();
        if (rows.Length != Expected.Length || rows.Any(row => !Expected.Any(doc => doc.Path == row["relativePath"]?.GetValue<string>())))
            issues.Add("delivery.manifest_inventory");
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var doc in Expected)
        {
            _ = clock.Work;
            var entries = rows.Where(row => row["relativePath"]?.GetValue<string>() == doc.Path).ToArray();
            if (entries.Length != 1) { issues.Add("delivery.manifest_missing_or_duplicate:" + doc.Path); continue; }
            var row = entries[0]; var target = Path.Combine(pack, doc.Path); var source = Path.Combine(resources, doc.Path);
            if (row["role"]?.GetValue<string>() != doc.Role || row["sourcePath"]?.GetValue<string>() != source || row["sessionPath"]?.GetValue<string>() != target)
                issues.Add("delivery.binding:" + doc.Path);
            if (!File.Exists(target)) { issues.Add("delivery.copy_missing:" + doc.Path); continue; }
            if (!File.Exists(source) || !File.Exists(Path.Combine(repo, doc.Path))) { issues.Add("delivery.source_missing:" + doc.Path); continue; }
            var bytes = File.ReadAllBytes(target);
            if (!bytes.SequenceEqual(File.ReadAllBytes(source)) || !bytes.SequenceEqual(File.ReadAllBytes(Path.Combine(repo, doc.Path))))
                issues.Add("delivery.bytes:" + doc.Path);
            result[doc.Path] = bytes;
        }
        return result;
    }
    private static ProcessStat ReadStat(int pid)
    {
        var stat = File.ReadAllText($"/proc/{pid}/stat");
        var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new(pid, long.Parse(fields[19], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture), fields[0]);
    }
    private static string[] ReadArgs(int pid) => Encoding.UTF8.GetString(File.ReadAllBytes($"/proc/{pid}/cmdline"))
        .Split('\0', StringSplitOptions.RemoveEmptyEntries);
    private static bool Terminal(ProcessStat stat) => stat.State is "Z" or "X" or "x";
    private static ProcessStat? ReadOwnedStat(int pid)
    {
        try { return ReadStat(pid); }
        catch (FileNotFoundException) when (!Directory.Exists($"/proc/{pid}")) { return null; }
        catch (DirectoryNotFoundException) when (!Directory.Exists($"/proc/{pid}")) { return null; }
    }
    private static Identity ReadIdentity(int pid, string role, JsonArray? observations = null, string[]? expectedArgs = null)
    {
        var row = new JsonObject { ["phase"] = "initial_live_identity", ["pid"] = pid,
            ["expectedArgs"] = System.Text.Json.JsonSerializer.SerializeToNode(expectedArgs) };
        var order = new JsonArray(); row["readOrder"] = order;
        try
        {
            order.Add("statBefore");
            var before = ReadStat(pid); row["statBefore"] = System.Text.Json.JsonSerializer.SerializeToNode(before);
            order.Add("cmdline");
            var args = ReadArgs(pid);
            order.Add("statAfter");
            var after = ReadStat(pid); row["statAfter"] = System.Text.Json.JsonSerializer.SerializeToNode(after);
            var expectedInvocation = expectedArgs is null || (args.Length > 0 && Path.GetFileName(args[0]) == "pwsh" &&
                args.Skip(1).SequenceEqual(expectedArgs));
            row["observedArgs"] = before.BirthTicks == after.BirthTicks && expectedInvocation
                ? System.Text.Json.JsonSerializer.SerializeToNode(args) : null;
            row["argvRedacted"] = before.BirthTicks != after.BirthTicks || !expectedInvocation;
            if (!expectedInvocation || before.BirthTicks != after.BirthTicks || Terminal(before) || Terminal(after) || args.Length == 0)
                throw new IOException("delivery.initial_identity_not_stable_live");
            row["classification"] = "LiveCandidate";
            return new(pid, after.BirthTicks, after.ParentPid, args, role, after.State);
        }
        catch (Exception error) { row["failure"] = error.ToString(); throw; }
        finally { if (observations is not null) observations.Add(row); }
    }
    private static bool ObserveOwned(Identity expected, Process? managed, OwnershipEvidence evidence, Clock clock, string phase)
    {
        var row = new JsonObject { ["phase"] = phase, ["expected"] = System.Text.Json.JsonSerializer.SerializeToNode(expected) };
        var order = new JsonArray(); row["readOrder"] = order;
        bool Stop(string classification) { row["classification"] = classification; return false; }
        ProcessStat? Stat(string name)
        {
            _ = clock.Operation; order.Add(name);
            var value = ReadOwnedStat(expected.Pid); row[name] = System.Text.Json.JsonSerializer.SerializeToNode(value);
            if (value is not null && value.BirthTicks != expected.BirthTicks)
                throw new InvalidDataException("delivery.owned_birth_mismatch");
            return value;
        }
        bool? ManagedExit(string name)
        {
            _ = clock.Operation; order.Add(name);
            bool? value = managed?.HasExited; row[name] = value is null ? null : JsonValue.Create(value.Value); return value;
        }
        try
        {
            var before = Stat("statBefore");
            if (before is null)
            {
                var absentAgain = Stat("statAbsentConfirmation");
                var exit = ManagedExit("managedExitedAfterAbsence");
                if (absentAgain is null && (managed is null || exit == true)) return Stop("TerminalOwnedAbsent");
                throw new InvalidDataException("delivery.owned_absence_unproved");
            }
            _ = clock.Operation; order.Add("cmdline");
            string[]? args;
            try { args = ReadArgs(expected.Pid); }
            catch (FileNotFoundException) when (!Directory.Exists($"/proc/{expected.Pid}")) { args = null; }
            catch (DirectoryNotFoundException) when (!Directory.Exists($"/proc/{expected.Pid}")) { args = null; }
            // Do not emit argv from a reused/unproved birth; preserve both sides of the owned observation window.
            var after = Stat("statAfter");
            row["observedArgs"] = System.Text.Json.JsonSerializer.SerializeToNode(args);
            var managedExited = ManagedExit("managedExitedAfterStat");
            // A nonempty mismatch is sticky even if this same observation also proves later termination.
            if (args is { Length: > 0 } && !args.SequenceEqual(expected.Args))
                throw new InvalidDataException("delivery.retained_identity_changed");
            if (after is null)
            {
                if (Stat("statAbsentConfirmation") is null && (managed is null || managedExited == true))
                    return Stop("TerminalOwnedAbsent");
                throw new InvalidDataException("delivery.owned_absence_unproved");
            }
            if (Terminal(after))
            {
                var confirmed = Terminal(before) ? before : Stat("statTerminalConfirmation");
                if (confirmed is null || Terminal(confirmed)) return Stop("TerminalOwnedState");
                throw new InvalidDataException("delivery.owned_terminal_unproved");
            }
            if (Terminal(before)) throw new InvalidDataException("delivery.owned_terminal_transition_conflict");
            if (managedExited == true) return Stop("TerminalOwnedManagedExit");
            if (args is null || args.Length == 0)
                throw new InvalidDataException("delivery.owned_live_argv_unproved");
            row["classification"] = "LiveExact"; return true;
        }
        catch (Exception error)
        {
            evidence.Unresolved = true; row["classification"] = "Conflict"; row["failure"] = error.ToString(); throw;
        }
        finally { evidence.Observations.Add(row); }
    }
    private static void Census(string package, string root,
        Dictionary<(int, long), Identity> authorized, Dictionary<(int, long), Identity> unknown, List<string> issues,
        OwnershipEvidence evidence, Process? child, Clock clock)
    {
        var live = new List<Identity>();
        foreach (var directory in Directory.EnumerateDirectories("/proc").Where(path => int.TryParse(Path.GetFileName(path), out _)))
        {
            _ = clock.Operation;
            Identity identity;
            try { identity = ReadIdentity(int.Parse(Path.GetFileName(directory)), "helper"); }
            catch (IOException) { continue; } // Unstable/terminal foreign candidates grant no ownership; retained identities checked below.
            catch (UnauthorizedAccessException) { continue; }
            live.Add(identity);
            var exactHelper = identity.Args.Length == 5 && Path.GetFileName(identity.Args[0]) == "dotnet" &&
                identity.Args.Skip(1).SequenceEqual(new[] { Path.Combine(package, "BookOfEternityClient.dll"), "--gm-main-operation", "--root", root });
            if (exactHelper) authorized.TryAdd((identity.Pid, identity.BirthTicks), identity);
        }
        var descendants = authorized.Values.Where(identity => ObserveOwned(identity, identity.Role == "pwsh" ? child : null,
            evidence, clock, "census_owned_check")).Select(identity => identity.Pid).ToHashSet();
        bool added;
        do
        {
            added = false;
            foreach (var identity in live.Where(identity => descendants.Contains(identity.ParentPid) && !descendants.Contains(identity.Pid)))
            {
                descendants.Add(identity.Pid); added = true;
                if (authorized.ContainsKey((identity.Pid, identity.BirthTicks))) continue;
                if (unknown.TryAdd((identity.Pid, identity.BirthTicks), identity with { Args = Array.Empty<string>(), Role = "unknown-descendant" }))
                    issues.Add("delivery.unexpected_descendant:" + identity.Pid);
            }
        } while (added);
    }
}
