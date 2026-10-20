// THROWAWAY DIAGNOSTIC: retained solely for evidence/reproduction of spec13.
// No product references are copied or rebuilt. All issuers are accepted compiled APIs.
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Measures a bounded genuine snapshot reader with one originally owned Windows child.
/// </summary>
internal static class SnapshotProbe
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private const string Soul = "game_state/meta/soul_state.json";
    private const string SoulHash = "19E66A7A2059C8F569862E55FE728B6A6A3BA62C93FDBE42265E821B7DE68430";
    private const string Manifest = "game_state/control/pending_turn_snapshot.json";
    private const string Authority = "game_state/control/pending_turn_snapshot.authority.json";
    private static Assembly _game = null!;
    private static Assembly _integration = null!;

    /// <summary>
    /// Dispatches metadata inspection or the single bounded diagnostic attempt.
    /// </summary>
    /// <param name="args">
    /// Describe BIN, parent BIN OWN SOUL EVIDENCE_DIR, or the parent's gated child arguments.
    /// </param>
    /// <returns>
    /// Zero for completed requested oracles; one for any failure.
    /// </returns>
    private static async Task<int> Main(string[] args)
    {
        try
        {
            Require(args.Length >= 2, "Missing mode/BIN.");
            Load(args[1]);
            if (args[0] == "describe" && args.Length == 2)
            {
                Describe();
                return 0;
            }
            if (args[0] == "parent" && args.Length == 5)
                return await Parent(args[1], args[2], args[3], args[4]);
            if (args[0] == "child" && args.Length == 4)
                return await Child(args[2], args[3]);
            throw new ArgumentException("Expected describe BIN | parent BIN OWN SOUL EVIDENCE_DIR.");
        }
        catch (Exception ex)
        {
            Emit(new { kind = "failure", error = ex.ToString() });
            return 1;
        }
    }

    /// <summary>
    /// Installs an explicit read-only resolver for the accepted binary directory.
    /// </summary>
    /// <param name="bin">
    /// Existing absolute accepted integration output directory.
    /// </param>
    private static void Load(string bin)
    {
        Require(Path.IsPathFullyQualified(bin) && Directory.Exists(bin), "BIN must exist and be absolute.");
        bin = Path.GetFullPath(bin);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            Require(name.Name is not null && Path.GetFileName(name.Name) == name.Name, "Invalid assembly name.");
            var path = Path.Combine(bin, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        _game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bin, "BookOfEternityClient.dll"));
        _integration = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bin, "BookOfEternityClient.IntegrationTests.dll"));
    }

    /// <summary>
    /// Checks selected compiled signatures without invoking any fixture or game API.
    /// </summary>
    private static void Describe()
    {
        var engine = Type("BookOfEternityClient.Core.GameEngine");
        var files = Type("BookOfEternityClient.Core.FileSystemManager");
        var fixture = _integration.GetType("BookOfEternityClient.Tests.GameEngineTurnLifecycleTests", throwOnError: true)!;
        var owner = Type("BookOfEternityClient.Services.GmWorkers.GmWorkerWindowsOwnedLaunch");
        var settingsMethod = Method(fixture, "InertRealmSettings", 1);
        var configureType = Method(fixture, "CreateGameEngine", 6).GetParameters()[1].ParameterType;
        Require(settingsMethod.IsStatic && settingsMethod.GetParameters()[0].ParameterType == configureType.GenericTypeArguments[0], "Inert settings delegate signature changed.");
        var requiredMembers = new List<string>();
        foreach (var entry in new (Type Type, string[] Properties)[]
        {
            (files, ["GameSessionPath"]),
            (Type("BookOfEternityClient.Core.FileSystemManagerHooks"), ["AfterCanonicalReadInitialValidationAsync"]),
            (Type("BookOfEternityClient.Core.PhysicalLoadTransactionOperations"), ["Instance"]),
            (Type("BookOfEternityClient.Configuration.SharedJsonOptions"), ["PrettyCamelCaseUnsafeRelaxed"]),
            (Type("BookOfEternityClient.Configuration.GameSettings"), ["GmBridgeAutoStart", "MusicEnabled", "SoundEnabled"]),
            (Type("BookOfEternityClient.Models.TurnRequest"), ["SessionId", "RequestId", "TurnNumber", "PlayerAction", "Timestamp", "PreGeneratedDices1d20", "ProgressionControl"]),
            (Type("BookOfEternityClient.Models.ProgressionControl"), ["CurrentRealm"]),
            (Type("BookOfEternityClient.Services.PendingTurnSnapshotReadAuthority"), ["SessionId", "RequestId", "SnapshotToken", "TurnNumber", "Realm", "CoveredLogicalPaths", "AbsentLogicalPaths"]),
            (owner, ["RetainsAssignedWindowsJob", "HostExited"]),
            (Type("BookOfEternityClient.Services.GmWorkers.GmWorkerProcessTreeFactory"), ["Instance"]),
            (Type("BookOfEternityClient.Services.GmWorkers.GmWorkerStopEvidence"), ["CleanupComplete", "State", "RootExitCode"]),
            (Type("BookOfEternityClient.Services.GmWorkers.GmWorkerOwnedOutputs"), ["StandardOutput", "StandardError"]),
            (Type("BookOfEternityClient.Services.AudioService"), ["Status"]),
            (Type("BookOfEternityClient.Services.AudioStatus"), ["Outcome"])
        })
            foreach (var name in entry.Properties)
                requiredMembers.Add((entry.Type.GetProperty(name, All) ?? throw new MissingMemberException(entry.Type.FullName, name)).ToString()!);
        Require(engine.GetField("_audioService", All)?.FieldType == Type("BookOfEternityClient.Services.AudioService"), "Engine audio field changed.");
        Require(Type("BookOfEternityClient.Services.MortalQteWoundOpportunityProjection").GetField("Paths", All)?.FieldType == typeof(string[]), "Projection Paths metadata changed.");
        Require(files.GetConstructors(All).Count(c => c.GetParameters().Length == 4) == 1, "Hooked filesystem constructor changed.");
        var nullLogger = Assembly.Load("Microsoft.Extensions.Logging.Abstractions").GetType("Microsoft.Extensions.Logging.Abstractions.NullLogger`1", throwOnError: true)!.MakeGenericType(files);
        Require(nullLogger.GetProperty("Instance", All) is not null || nullLogger.GetField("Instance", All) is not null, "NullLogger Instance metadata missing.");
        var signatures = new[]
        {
            Method(engine, "CreatePreTurnBackup", 1).ToString(),
            Method(engine, "CreateCanonicalBaselineSnapshotAsync", 4).ToString(),
            Method(files, "AcquireCanonicalWriteLeaseAsync", 3).ToString(),
            files.GetMethods(All).Single(m => m.Name == "ReadFileBytesAsync" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.Name == "CanonicalWriteLease").ToString(),
            Method(Type("BookOfEternityClient.Services.MortalQteWoundOpportunityProjection"), "ReadSnapshot", 2).ToString(),
            Method(fixture, "CreateGameEngine", 6).ToString(), settingsMethod.ToString(),
            fixture.GetConstructors(All).Single(c => c.GetParameters().Length == 2).ToString(),
            owner.GetConstructor(All, null, [typeof(ProcessStartInfo)], null)!.ToString(),
            Method(owner, "Start", 0).ToString(), Method(owner, "Attach", 1).ToString(),
            Method(owner, "StopAndObserveAsync", 0).ToString(), Method(owner, "SettleOutputsAsync", 0).ToString()
        };
        Require(signatures.All(s => s is not null), "Missing expected signature.");
        Emit(new { kind = "describe", diagnostic = "THROWAWAY DIAGNOSTIC", fixtures = 0, children = 0, gameCalls = 0,
            game = _game.Location, integration = _integration.Location, signatures, requiredMembers });
    }

    /// <summary>
    /// Starts exactly one gated child under the original production Windows owner and settles it.
    /// </summary>
    /// <param name="bin">
    /// Accepted binary directory passed unchanged to the child.
    /// </param>
    /// <param name="own">
    /// Nonexistent strict temporary descendant named boe-spec13-probe-GUID.
    /// </param>
    /// <param name="soulPath">
    /// Immutable 415-byte source DATA path.
    /// </param>
    /// <param name="evidence">
    /// Existing caller-owned result directory, outside the owned child root.
    /// </param>
    /// <returns>
    /// Zero only when the child succeeds and original-owner cleanup, EOF, join and deletion succeed.
    /// </returns>
    private static async Task<int> Parent(string bin, string own, string soulPath, string evidence)
    {
        var elapsed = Stopwatch.StartNew();
        own = ValidateOwn(own, mustExist: false);
        Require(OperatingSystem.IsWindows(), "Windows ownership is required.");
        Require(Path.IsPathFullyQualified(evidence) && Directory.Exists(evidence), "EVIDENCE_DIR must already exist.");
        evidence = Path.GetFullPath(evidence);
        Require(!IsUnder(evidence, own) && !IsUnder(own, evidence), "Evidence and OWN cannot overlap.");
        ValidateOrdinaryAncestors(evidence);
        ValidateSoul(soulPath);
        foreach (var name in new[] { "parent.json", "child.stdout", "child.stderr" })
            Require(!File.Exists(Path.Combine(evidence, name)), "Evidence outputs must not already exist.");
        Directory.CreateDirectory(own);
        ValidateOrdinaryAncestors(own);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var value in new[] { Assembly.GetExecutingAssembly().Location, "child", bin, own, soulPath }) start.ArgumentList.Add(value);
        object? owner = null;
        object? stop = null;
        var failures = new List<string>();
        var stdout = "";
        var stderr = "";
        var attached = false;
        var joined = false;
        var eof = false;
        var disposed = false;
        var absent = false;
        int? exitCode = null;
        int? ownedPid = null;
        try
        {
            var ownerType = Type("BookOfEternityClient.Services.GmWorkers.GmWorkerWindowsOwnedLaunch");
            owner = ownerType.GetConstructor(All, null, [typeof(ProcessStartInfo)], null)!.Invoke([start]);
            Invoke(owner, "Start");
            ownedPid = (int?)GetNullable(owner, "AdmittedHostProcessId");
            var factory = Get(Type("BookOfEternityClient.Services.GmWorkers.GmWorkerProcessTreeFactory"), "Instance");
            Invoke(owner, "Attach", factory);
            attached = (bool)Get(owner, "RetainsAssignedWindowsJob");
            Require(attached, "Original owner did not retain assigned Windows Job.");
            File.WriteAllText(Path.Combine(own, "release.gate"), "original-owner-job-attached");
            await ((Task)Get(owner, "HostExited")).WaitAsync(TimeSpan.FromSeconds(Math.Max(0.001, 30 - elapsed.Elapsed.TotalSeconds)));
            joined = true;
        }
        catch (Exception ex) { failures.Add(ex.ToString()); }
        finally
        {
            if (owner is not null)
            {
                try
                {
                    stop = await Result((Task)Invoke(owner, "StopAndObserveAsync"), 10);
                    Require((bool)Get(stop, "CleanupComplete") && Get(stop, "State").ToString() == "StoppedWithinScope", "Original stop did not confirm complete scope.");
                    exitCode = (int?)GetNullable(stop, "RootExitCode");
                    await ((Task)Get(owner, "HostExited")).WaitAsync(TimeSpan.FromSeconds(10));
                    joined = true;
                    var outputs = await Result((Task)Invoke(owner, "SettleOutputsAsync"), 10);
                    stdout = (string)Get(outputs, "StandardOutput");
                    stderr = (string)Get(outputs, "StandardError");
                    eof = true;
                    await ((IAsyncDisposable)owner).DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                    disposed = true;
                }
                catch (Exception ex) { failures.Add("cleanup: " + ex); }
            }
        }
        if (exitCode != 0) failures.Add("Child exit code: " + exitCode);
        if (joined && eof && disposed && exitCode == 0 && !File.Exists(Path.Combine(own, "cleanup.uncertain")))
        {
            try
            {
                Require(ValidateOwn(own, true) == own, "Owned root changed.");
                ValidateTree(own);
                Directory.Delete(own, recursive: true);
                absent = !Directory.Exists(own) && !File.Exists(own);
                Require(absent, "Owned root still exists.");
            }
            catch (Exception ex) { failures.Add("owned-root deletion: " + ex); }
        }
        Require(elapsed.Elapsed < TimeSpan.FromMinutes(2), "Parent exceeded two-minute boundary.");
        var report = new { kind = "parent", diagnostic = "THROWAWAY DIAGNOSTIC", own, ownedPid, attached, joined, eof, disposed, absent, exitCode,
            elapsedMs = elapsed.Elapsed.TotalMilliseconds, success = failures.Count == 0 && absent, failures, stop };
        File.WriteAllText(Path.Combine(evidence, "child.stdout"), stdout);
        File.WriteAllText(Path.Combine(evidence, "child.stderr"), stderr);
        File.WriteAllText(Path.Combine(evidence, "parent.json"), JsonSerializer.Serialize(report));
        Emit(report);
        return failures.Count == 0 && absent ? 0 : 1;
    }

    /// <summary>
    /// Waits for original-owner attachment and runs both fixture oracles within a total thirty seconds.
    /// </summary>
    /// <param name="own">
    /// The parent's existing validated private temporary container.
    /// </param>
    /// <param name="soulPath">
    /// Exact immutable input path whose bytes are validated before copying.
    /// </param>
    /// <returns>
    /// Zero only after both genuine fixture oracles finish.
    /// </returns>
    private static async Task<int> Child(string own, string soulPath)
    {
        var elapsed = Stopwatch.StartNew();
        own = ValidateOwn(own, true);
        while (!File.Exists(Path.Combine(own, "release.gate")))
        {
            Require(elapsed.Elapsed < TimeSpan.FromSeconds(30), "Child attachment gate timeout.");
            await Task.Delay(10);
        }
        var bytes = ValidateSoul(soulPath);
        foreach (var expanded in new[] { false, true })
        {
            var remaining = TimeSpan.FromSeconds(30) - elapsed.Elapsed;
            Require(remaining > TimeSpan.Zero, "Child total budget exhausted before fixture.");
            await Fixture(own, bytes, expanded).WaitAsync(remaining);
        }
        Require(elapsed.Elapsed < TimeSpan.FromSeconds(30), "Child total budget exceeded.");
        Emit(new { kind = "child", success = true, pid = Environment.ProcessId, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, elapsedMs = elapsed.Elapsed.TotalMilliseconds, soulSha256 = Hash(bytes) });
        return 0;
    }

    /// <summary>
    /// Creates a genuine engine-issued original, observes positives and controls, then checks the expanded tamper refusal.
    /// </summary>
    /// <param name="own">
    /// Original parent's private temporary container.
    /// </param>
    /// <param name="soulBytes">
    /// Verified exact soul DATA, copied only into the new fixture.
    /// </param>
    /// <param name="expanded">
    /// True adds sixteen inert lore members and executes the rollback-byte negative oracle.
    /// </param>
    /// <returns>
    /// Completion only after every applicable oracle and explicit audio disposal succeeds.
    /// </returns>
    private static async Task Fixture(string own, byte[] soulBytes, bool expanded)
    {
        var root = Path.Combine(own, expanded ? "expanded" : "minimal");
        Require(!Directory.Exists(root) && !File.Exists(root), "Fixture already exists.");
        Directory.CreateDirectory(root);
        object? engine = null;
        object? lease = null;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var fixtureType = _integration.GetType("BookOfEternityClient.Tests.GameEngineTurnLifecycleTests", throwOnError: true)!;
        var filesType = Type("BookOfEternityClient.Core.FileSystemManager");
        var hooksType = Type("BookOfEternityClient.Core.FileSystemManagerHooks");
        var hooks = Activator.CreateInstance(hooksType, nonPublic: true)!;
        Func<string, Task> observer = path => { counts[path] = counts.GetValueOrDefault(path) + 1; return Task.CompletedTask; };
        hooksType.GetProperty("AfterCanonicalReadInitialValidationAsync", All)!.SetValue(hooks, observer);
        var loggerType = Assembly.Load("Microsoft.Extensions.Logging.Abstractions").GetType("Microsoft.Extensions.Logging.Abstractions.NullLogger`1")!.MakeGenericType(filesType);
        var operationsType = Type("BookOfEternityClient.Core.PhysicalLoadTransactionOperations");
        var files = filesType.GetConstructors(All).Single(c => c.GetParameters().Length == 4).Invoke([root, Get(loggerType, "Instance"), Get(operationsType, "Instance"), hooks]);
        try
        {
            Invoke(files, "EnsureDirectoryStructure");
            var sessionRoot = (string)Get(files, "GameSessionPath");
            WriteData(sessionRoot, Soul, soulBytes);
            for (var i = 0; i < (expanded ? 16 : 0); i++)
                WriteData(sessionRoot, $"lore/probe/member_{i:00}.json", System.Text.Encoding.UTF8.GetBytes($"{{\"diagnosticMember\":{i}}}"));
            var fixture = fixtureType.GetConstructors(All).Single(c => c.GetParameters().Length == 2).Invoke([null, root]);
            // Fixture.Dispose only deletes its root; keep it uncalled until original owner cleanup.
            var configureType = Method(fixtureType, "CreateGameEngine", 6).GetParameters()[1].ParameterType;
            var configure = Method(fixtureType, "InertRealmSettings", 1).CreateDelegate(configureType);
            var originalOut = Console.Out;
            try
            {
                Console.SetOut(Console.Error);
                engine = Method(fixtureType, "CreateGameEngine", 6).Invoke(fixture, [null, configure, null, files, null, null])!;
            }
            finally { Console.SetOut(originalOut); }
            var requestType = Type("BookOfEternityClient.Models.TurnRequest");
            var request = Activator.CreateInstance(requestType)!;
            var session = Guid.NewGuid().ToString("N");
            var requestId = Guid.NewGuid().ToString("N");
            Set(request, "SessionId", session); Set(request, "RequestId", requestId); Set(request, "TurnNumber", 1);
            Set(request, "PlayerAction", "spec13 bounded snapshot diagnostic"); Set(request, "Timestamp", "2026-10-20T00:00:00.0000000Z");
            Set(request, "PreGeneratedDices1d20", new[] { 3, 17 });
            var progression = Activator.CreateInstance(Type("BookOfEternityClient.Models.ProgressionControl"))!;
            Set(progression, "CurrentRealm", "Mortal World"); Set(request, "ProgressionControl", progression);
            var rollback = await Result((Task)Method(engine.GetType(), "CreatePreTurnBackup", 1).Invoke(engine, [requestId])!);
            await (Task)Method(engine.GetType(), "CreateCanonicalBaselineSnapshotAsync", 4).Invoke(engine, [request, rollback, "THROWAWAY DIAGNOSTIC spec13", null])!;
            var options = (JsonSerializerOptions)Get(Type("BookOfEternityClient.Configuration.SharedJsonOptions"), "PrettyCamelCaseUnsafeRelaxed");
            WriteData(sessionRoot, "input/turn_request.json", JsonSerializer.SerializeToUtf8Bytes(request, requestType, options));
            var manifestBytes = File.ReadAllBytes(SafeDataPath(sessionRoot, Manifest));
            var authorityBytes = File.ReadAllBytes(SafeDataPath(sessionRoot, Authority));
            var manifest = JsonNode.Parse(manifestBytes)!.AsObject();
            var map = manifest["files"]!.AsObject();
            var backups = manifest["rollbackBackups"]!.AsObject();
            var expected = new[] { Soul }.Concat(Enumerable.Range(0, expanded ? 16 : 0).Select(i => $"lore/probe/member_{i:00}.json")).Order(StringComparer.Ordinal).ToArray();
            Require(map.Select(p => p.Key).Order(StringComparer.Ordinal).SequenceEqual(expected), "Unexpected actual Files membership.");
            Require(backups.Select(p => p.Key).Order(StringComparer.Ordinal).SequenceEqual(expected), "Unexpected actual RollbackBackups membership.");
            var token = manifest["manifestPayloadHash"]!.GetValue<string>();
            var before = Inventory(sessionRoot);
            var acquire = Method(filesType, "AcquireCanonicalWriteLeaseAsync", 3);
            // Invoke synchronously in THIS execution flow: production installs AsyncLocal before returning its task.
            var leaseTask = (Task)acquire.Invoke(files, [Enum.Parse(acquire.GetParameters()[0].ParameterType, "SessionMutation"), CancellationToken.None, null])!;
            await leaseTask;
            lease = Get(leaseTask, "Result");
            var projection = Type("BookOfEternityClient.Services.MortalQteWoundOpportunityProjection");
            var optional = (string[])projection.GetField("Paths", All)!.GetValue(null)!;
            Require(optional.Length == 9, "Projection optional contract changed.");
            var observations = new List<object>();
            for (var i = 0; i < 8; i++)
            {
                counts.Clear();
                var watch = Stopwatch.StartNew();
                var read = Invoke(projection, "ReadSnapshot", files, lease);
                var ms = watch.Elapsed.TotalMilliseconds;
                Require((string)Get(read, "SessionId") == session && (string)Get(read, "RequestId") == requestId && (string)Get(read, "SnapshotToken") == token && (int)Get(read, "TurnNumber") == 1 && (string)Get(read, "Realm") == "mortal_world", "Snapshot binding mismatch.");
                Require(((IEnumerable)Get(read, "CoveredLogicalPaths")).Cast<string>().SequenceEqual([Soul]), "Covered paths mismatch.");
                Require(((IEnumerable)Get(read, "AbsentLogicalPaths")).Cast<string>().Order(StringComparer.Ordinal).SequenceEqual(optional.Order(StringComparer.Ordinal)), "Observed absences mismatch.");
                Require(((byte[])Invoke(read, "ReadRequiredBytes", Soul)).SequenceEqual(soulBytes), "Selected bytes mismatch.");
                var readCounts = new Dictionary<string, int>(counts, StringComparer.Ordinal);
                Require(backups.All(p => readCounts.GetValueOrDefault(p.Value!.GetValue<string>()) >= 1), "A rollback physical read was not observed.");
                observations.Add(new { call = i, elapsedMs = ms, totalReads = readCounts.Values.Sum(), paths = readCounts });
            }
            counts.Clear();
            var rawSoulPath = SafeDataPath(sessionRoot, Soul);
            var rawWatch = Stopwatch.StartNew();
            for (var i = 0; i < 16; i++) Require(File.ReadAllBytes(rawSoulPath).SequenceEqual(soulBytes), "Raw control bytes mismatch.");
            var rawMs = rawWatch.Elapsed.TotalMilliseconds;
            Require(counts.Count == 0, "Raw control unexpectedly called production hook.");
            var rawCounts = new Dictionary<string, int>(counts);
            counts.Clear();
            var leasedMethod = filesType.GetMethods(All).Single(m => m.Name == "ReadFileBytesAsync" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.Name == "CanonicalWriteLease");
            var leasedWatch = Stopwatch.StartNew();
            for (var i = 0; i < 16; i++)
                Require(((byte[])await Result((Task)leasedMethod.Invoke(files, [lease, Soul])!)).SequenceEqual(soulBytes), "Leased control bytes mismatch.");
            var leasedMs = leasedWatch.Elapsed.TotalMilliseconds;
            var leasedCounts = new Dictionary<string, int>(counts);
            Require(leasedCounts.Count == 1 && leasedCounts.GetValueOrDefault(Soul) == 16, "Leased control read observations mismatch.");
            var after = Inventory(sessionRoot);
            Require(before.SequenceEqual(after), "Positive game_session inventory/hash changed.");
            object? negative = null;
            if (expanded)
            {
                var physical = backups["lore/probe/member_00.json"]!.GetValue<string>();
                var tamperPath = SafeDataPath(sessionRoot, physical);
                using (var append = new FileStream(tamperPath, FileMode.Append, FileAccess.Write, FileShare.None)) append.WriteByte(0x20);
                counts.Clear();
                var negativeWatch = Stopwatch.StartNew();
                Exception? rejected = null;
                try { Invoke(projection, "ReadSnapshot", files, lease); }
                catch (InvalidDataException ex) when (ex.Message.Contains("pending_turn_snapshot_reader_authority_invalid", StringComparison.Ordinal)) { rejected = ex; }
                Require(rejected is not null, "Tampered unselected rollback was not rejected by expected authority failure.");
                Require(File.ReadAllBytes(SafeDataPath(sessionRoot, Manifest)).SequenceEqual(manifestBytes) && File.ReadAllBytes(SafeDataPath(sessionRoot, Authority)).SequenceEqual(authorityBytes), "Negative changed manifest/authority.");
                var changed = Inventory(sessionRoot).Where(p => !before.TryGetValue(p.Key, out var hash) || hash != p.Value).Select(p => p.Key).ToArray();
                Require(changed.SequenceEqual([physical]) && Inventory(sessionRoot).Count == before.Count, "Negative changed more than its physical backup.");
                negative = new { physical, rejected = true, error = rejected!.Message, elapsedMs = negativeWatch.Elapsed.TotalMilliseconds, paths = new Dictionary<string, int>(counts), manifestUnchanged = true, authorityUnchanged = true };
            }
            Emit(new { kind = "fixture", variant = expanded ? "expanded" : "minimal", success = true, session, requestId, token, turn = 1, realm = "mortal_world",
                soulSha256 = Hash(soulBytes), soulLength = soulBytes.Length, files = map.Count, rollbackBackups = backups.Count, expectedPhysicalScale = backups.Count + 4,
                manifestSha256 = Hash(manifestBytes), authoritySha256 = Hash(authorityBytes), selectedCovered = new[] { Soul }, observedAbsent = optional,
                positiveInventoryUnchanged = true, inventory = before, observations, raw = new { calls = 16, elapsedMs = rawMs, paths = rawCounts }, leased = new { calls = 16, elapsedMs = leasedMs, paths = leasedCounts }, negative });
        }
        finally
        {
            var cleanupErrors = new List<Exception>();
            try
            {
                if (lease is IAsyncDisposable asyncLease) await asyncLease.DisposeAsync();
                else if (lease is IDisposable disposableLease) disposableLease.Dispose();
            }
            catch (Exception ex) { cleanupErrors.Add(ex); }
            try
            {
                if (engine is not null)
                {
                    var audio = engine.GetType().GetField("_audioService", All)!.GetValue(engine)!;
                    await ((IAsyncDisposable)audio).DisposeAsync();
                    Require(Get(Get(audio, "Status"), "Outcome").ToString() == "Disposed", "Audio disposal retained cleanup uncertainty.");
                }
            }
            catch (Exception ex) { cleanupErrors.Add(ex); }
            if (cleanupErrors.Count != 0)
            {
                File.WriteAllText(Path.Combine(own, "cleanup.uncertain"), "lease/audio disposal failed; preserve owned root");
                throw new AggregateException("Fixture lease/audio cleanup failed.", cleanupErrors);
            }
            Emit(new { kind = "fixtureCleanup", variant = expanded ? "expanded" : "minimal", audioDisposed = engine is not null, leaseReleased = lease is not null });
        }
    }

    /// <summary>
    /// Validates and reads the exact immutable soul input.
    /// </summary>
    /// <param name="path">
    /// Absolute existing ordinary source file.
    /// </param>
    /// <returns>
    /// The exact 415 verified bytes.
    /// </returns>
    private static byte[] ValidateSoul(string path)
    {
        Require(Path.IsPathFullyQualified(path) && File.Exists(path), "SOUL must be an absolute existing file.");
        ValidateOrdinaryAncestors(path);
        var bytes = File.ReadAllBytes(path);
        Require(bytes.Length == 415 && Hash(bytes) == SoulHash, "SOUL immutable DATA length/hash mismatch.");
        return bytes;
    }

    /// <summary>
    /// Validates the exclusive diagnostic temporary root and its existing ancestors.
    /// </summary>
    /// <param name="own">
    /// Absolute strict temporary descendant with the required GUID basename.
    /// </param>
    /// <param name="mustExist">
    /// True requires an existing directory; false requires no existing filesystem entry.
    /// </param>
    /// <returns>
    /// Normalized absolute root.
    /// </returns>
    private static string ValidateOwn(string own, bool mustExist)
    {
        Require(Path.IsPathFullyQualified(own), "OWN must be absolute.");
        own = Path.GetFullPath(own).TrimEnd(Path.DirectorySeparatorChar);
        const string prefix = "boe-spec13-probe-";
        var name = Path.GetFileName(own);
        Require(IsUnder(own, Path.GetTempPath()) && name.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParseExact(name[prefix.Length..], "N", out _), "OWN must be strict temp descendant boe-spec13-probe-GUID(N).");
        Require(mustExist ? Directory.Exists(own) : !Directory.Exists(own) && !File.Exists(own), "OWN existence contract failed.");
        ValidateOrdinaryAncestors(own);
        return own;
    }

    /// <summary>
    /// Refuses existing reparse points along an absolute path.
    /// </summary>
    /// <param name="path">
    /// File or directory whose ordinary ancestors are required.
    /// </param>
    private static void ValidateOrdinaryAncestors(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if (File.Exists(current) || Directory.Exists(current))
                Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Reparse point refused: " + current);
    }

    /// <summary>
    /// Checks every owned descendant before recursive deletion without traversing links.
    /// </summary>
    /// <param name="root">
    /// Validated existing owned directory.
    /// </param>
    private static void ValidateTree(string root)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            var attributes = File.GetAttributes(entry);
            Require((attributes & FileAttributes.ReparsePoint) == 0 && IsUnder(entry, root), "Unsafe owned descendant.");
            if ((attributes & FileAttributes.Directory) != 0) ValidateTree(entry);
        }
    }

    /// <summary>
    /// Determines strict normalized path containment.
    /// </summary>
    /// <param name="path">
    /// Candidate descendant.
    /// </param>
    /// <param name="root">
    /// Ancestor boundary.
    /// </param>
    /// <returns>
    /// True for a strict descendant; false for the root itself or an outside path.
    /// </returns>
    private static bool IsUnder(string path, string root) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves an ordinary logical file strictly within the new fixture session.
    /// </summary>
    /// <param name="root">
    /// Owned session root.
    /// </param>
    /// <param name="logical">
    /// Nonempty relative canonical path from generated metadata or fixed diagnostic input.
    /// </param>
    /// <returns>
    /// Validated absolute ordinary path.
    /// </returns>
    private static string SafeDataPath(string root, string logical)
    {
        Require(!string.IsNullOrWhiteSpace(logical) && !Path.IsPathRooted(logical), "Logical path must be relative.");
        var path = Path.GetFullPath(Path.Combine(root, logical.Replace('/', Path.DirectorySeparatorChar)));
        Require(IsUnder(path, root), "Logical path escaped fixture.");
        ValidateOrdinaryAncestors(path);
        return path;
    }

    /// <summary>
    /// Writes new fixture DATA only inside its owned session root.
    /// </summary>
    /// <param name="root">
    /// New owned session directory.
    /// </param>
    /// <param name="logical">
    /// Relative DATA target.
    /// </param>
    /// <param name="bytes">
    /// Exact bytes to create; existing files are refused.
    /// </param>
    private static void WriteData(string root, string logical, byte[] bytes)
    {
        var path = SafeDataPath(root, logical);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes);
    }

    /// <summary>
    /// Captures a sorted exact file inventory without following reparse points.
    /// </summary>
    /// <param name="root">
    /// Owned session root to inspect.
    /// </param>
    /// <returns>
    /// Logical file paths and uppercase SHA256 values.
    /// </returns>
    private static SortedDictionary<string, string> Inventory(string root)
    {
        ValidateTree(root);
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            result.Add(Path.GetRelativePath(root, file).Replace('\\', '/'), Hash(File.ReadAllBytes(file)));
        return result;
    }

    /// <summary>
    /// Computes the exact byte digest used in diagnostic evidence.
    /// </summary>
    /// <param name="bytes">
    /// Bytes to hash.
    /// </param>
    /// <returns>
    /// Uppercase SHA256 hexadecimal text.
    /// </returns>
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>
    /// Resolves a required accepted production type without constructing it.
    /// </summary>
    /// <param name="name">
    /// Exact fully qualified production type name.
    /// </param>
    /// <returns>
    /// Required compiled type metadata.
    /// </returns>
    private static Type Type(string name) => _game.GetType(name, throwOnError: true)!;

    /// <summary>
    /// Selects one required method by exact name and parameter count.
    /// </summary>
    /// <param name="type">
    /// Declaring compiled type.
    /// </param>
    /// <param name="name">
    /// Exact method name.
    /// </param>
    /// <param name="count">
    /// Exact argument count distinguishing accepted overloads.
    /// </param>
    /// <returns>
    /// Unique required method metadata; ambiguity fails closed.
    /// </returns>
    private static MethodInfo Method(Type type, string name, int count) => type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == count);

    /// <summary>
    /// Invokes an accepted method synchronously and unwraps its original exception.
    /// </summary>
    /// <param name="target">
    /// Instance, or Type for a static method.
    /// </param>
    /// <param name="name">
    /// Unique method name at the supplied arity.
    /// </param>
    /// <param name="args">
    /// Original exact arguments, including genuinely issued authority objects.
    /// </param>
    /// <returns>
    /// Method result, or a nonnull sentinel for void methods.
    /// </returns>
    private static object Invoke(object target, string name, params object?[] args)
    {
        try { return Method(target as Type ?? target.GetType(), name, args.Length).Invoke(target is Type ? null : target, args) ?? new object(); }
        catch (TargetInvocationException ex) when (ex.InnerException is not null) { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }

    /// <summary>
    /// Reads a required nonnull property or field without altering it.
    /// </summary>
    /// <param name="target">
    /// Instance, or Type for static metadata.
    /// </param>
    /// <param name="name">
    /// Exact member name.
    /// </param>
    /// <returns>
    /// Nonnull original member value.
    /// </returns>
    private static object Get(object target, string name) => GetNullable(target, name) ?? throw new InvalidDataException("Missing/null member: " + name);

    /// <summary>
    /// Reads an accepted property or field, preserving a null value.
    /// </summary>
    /// <param name="target">
    /// Instance, or Type for a static member.
    /// </param>
    /// <param name="name">
    /// Exact property or field name.
    /// </param>
    /// <returns>
    /// Original value, possibly null.
    /// </returns>
    private static object? GetNullable(object target, string name)
    {
        var type = target as Type ?? target.GetType();
        return type.GetProperty(name, All) is { } property ? property.GetValue(target is Type ? null : target) : type.GetField(name, All)!.GetValue(target is Type ? null : target);
    }

    /// <summary>
    /// Assigns an ordinary request DATA property.
    /// </summary>
    /// <param name="target">
    /// Newly constructed public request or progression DATA object.
    /// </param>
    /// <param name="name">
    /// Exact writable property.
    /// </param>
    /// <param name="value">
    /// Requested ordinary DATA value.
    /// </param>
    private static void Set(object target, string name, object value) => target.GetType().GetProperty(name, All)!.SetValue(target, value);

    /// <summary>
    /// Awaits an already created generic task and reads its genuine result.
    /// </summary>
    /// <param name="task">
    /// Original accepted API task; lease acquisition is deliberately not routed through this helper.
    /// </param>
    /// <param name="seconds">
    /// Optional finite cleanup wait, or null while the outer child deadline bounds setup/read work.
    /// </param>
    /// <returns>
    /// Original nonnull completed task result.
    /// </returns>
    private static async Task<object> Result(Task task, int? seconds = null)
    {
        if (seconds is { } limit) await task.WaitAsync(TimeSpan.FromSeconds(limit)); else await task;
        return Get(task, "Result");
    }

    /// <summary>
    /// Fails the diagnostic when an exact contract or oracle is violated.
    /// </summary>
    /// <param name="condition">
    /// True permits continuation; false throws a failure.
    /// </param>
    /// <param name="message">
    /// Concrete failure evidence.
    /// </param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    /// <summary>
    /// Emits one compact machine-readable JSON evidence record.
    /// </summary>
    /// <param name="record">
    /// Serializable metadata or completed oracle observations.
    /// </param>
    private static void Emit(object record) => Console.WriteLine(JsonSerializer.Serialize(record));
}
