using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(0)] // Prepared committed; I not published
    [InlineData(1)] // I member physically applied; second cohort not committed
    [InlineData(2)] // Published committed; cleanup/ack/owning release did not return, later culture rejects
    [InlineData(3)] // actual FIRST2 graph installed; no result, missing-result restart must hold
    [InlineData(4)] // ErrorPrepared committed; restart must report frozen errors and hold
    public async Task GuaranteedTreatment_ActualProcessLossReentersThroughCurrentOwnerWithoutReprojection(int cut)
        => await RunJointInterfaceActualProcessLossAsync(cut);

    [Theory]
    [InlineData("Version")]
    [InlineData("RecordFingerprint")]
    [InlineData("OriginalManifest")]
    [InlineData("CurrentInterface")]
    public async Task GuaranteedTreatment_CurrentResumeRefusesPartialSelectedIntegrityWithoutNormalizerFallback(string fault)
        => await RunJointInterfaceActualProcessLossAsync(0, fault);

    private async Task RunJointInterfaceActualProcessLossAsync(int cut, string? fault = null, bool diagnostic = false)
    {
        Assert.True(OperatingSystem.IsLinux());
        using var classOwned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var own = Path.Combine(Path.GetTempPath(), "boe-joint-interface-crash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(own, "tmp"));
        using var owned = new JointInterfaceCrashOwnedContainer(own, line => _directGachaOutput?.WriteLine(line));
        var nonce = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(own, "parent.json"), JsonSerializer.Serialize(new
        { Nonce = nonce, ParentPid = Environment.ProcessId, Cut = cut, Fault = fault, Diagnostic = diagnostic }));
        var cutPath = Path.Combine(own, "cut.json");
        var original = await RunJointInterfaceNativeChildAsync("engine-joint-interface-original-crash", own, cutPath, cut, own, owned);
        WritePhysicalInjuryDiagnostic("iobs_actual_abrupt_original_exit", original);
        Assert.Equal(73, original.ExitCode); // Environment.Exit, not an exception/catch/compensation surrogate
        Assert.True(original.Eof); Assert.True(original.Joined);
        var cutBytes = File.ReadAllBytes(cutPath);
        WritePhysicalInjuryDiagnostic("iobs_actual_abrupt_cut_raw_before_identity", new
        { Bytes = cutBytes.Length, Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(cutBytes)),
            Base64 = Convert.ToBase64String(cutBytes) });
        var atCut = ParseJsonObjectBytes(cutBytes);
        Assert.Equal(original.Pid, atCut["Pid"]!.GetValue<int>()); Assert.Equal(nonce, atCut["Nonce"]!.GetValue<string>());
        Assert.Equal(original.BirthTick, atCut["BirthTick"]!.GetValue<ulong>());
        var root = atCut["Root"]!.GetValue<string>();
        Assert.StartsWith(Path.GetFullPath(Path.Combine(own, "tmp")) + Path.DirectorySeparatorChar, Path.GetFullPath(root));
        Assert.True(Directory.Exists(root)); // abrupt original did not run managed fixture disposal
        Assert.Equal(0, atCut["CommonCallbacks"]!.GetValue<int>()); Assert.Equal(42, atCut["Turn"]!.GetValue<int>());
        Assert.NotNull(atCut["JournalBase64"]); Assert.Equal(cut, atCut["Cut"]!.GetValue<int>());
        var output = Path.Combine(own, "resume.json");
        var resumed = await RunJointInterfaceNativeChildAsync("engine-joint-interface-resume", root, output, cut, own, owned);
        var raw = File.Exists(output) ? File.ReadAllText(output) : null;
        WritePhysicalInjuryDiagnostic("iobs_actual_current_resume_exit", new
        { Original = original, Cut = atCut, Resume = resumed, ResultRaw = raw,
            OwnedContainer = own, ActualContainedRoots = Directory.EnumerateDirectories(Path.Combine(own, "tmp")).ToArray() });
        Assert.Equal(0, resumed.ExitCode); Assert.NotEqual(original.Pid, resumed.Pid);
        Assert.True(resumed.Eof); Assert.True(resumed.Joined);
        var result = JsonNode.Parse(Assert.IsType<string>(raw))!.AsObject();
        if (diagnostic) Assert.Null(result["DiagnosticOutputFailure"]);
        Assert.Equal(resumed.Pid, result["Pid"]!.GetValue<int>()); Assert.Equal(nonce, result["Nonce"]!.GetValue<string>());
        Assert.Equal(resumed.BirthTick, result["BirthTick"]!.GetValue<ulong>());
        Assert.True(result["ActualCallerEntered"]!.GetValue<bool>()); Assert.Null(result["Failure"]);
        Assert.Equal(cut < 3 && fault is null, result["Completed"]!.GetValue<bool>());
        Assert.Equal(cut >= 3 || fault is not null, result["Held"]!.GetValue<bool>());
        Assert.Equal(fault is null && cut < 2 ? 1 : 0, result["NewChangingICommits"]!.GetValue<int>());
        Assert.True(result["InertRepeatBytesEqual"]!.GetValue<bool>());
        Assert.True(result["FreshAuthorityFieldsAbsentBeforeEntry"]!.GetValue<bool>());
        if (cut == 2)
        {
            Assert.True(atCut["AmbientParseAccepted"]!.GetValue<bool>());
            Assert.False(result["AmbientParseAccepted"]!.GetValue<bool>());
            Assert.True(result["IndependentOrdinaryPolicyRejected"]!.GetValue<bool>());
            Assert.Equal("ParseAccepted", result["Result"]!["timestampCheck"]!["timestamp"]!.GetValue<string>());
        }
    }

    private sealed record JointInterfaceNativeOutcome(int Pid, DateTime BirthUtc, ulong BirthTick, int ExitCode, string Stdout, string Stderr,
        string Executable, string[] Arguments, object Dependencies, bool Eof, bool Joined);

    private static ulong ReadJointInterfaceLinuxBirthTick(int pid)
    {
        var stat = File.ReadAllText($"/proc/{pid}/stat");
        Assert.Equal(pid, int.Parse(stat.AsSpan(0, stat.IndexOf(' ')), CultureInfo.InvariantCulture));
        var close = stat.LastIndexOf(')'); // comm can contain spaces or parentheses
        Assert.True(close > 0 && close + 2 < stat.Length && stat[close + 1] == ' ');
        var fields = stat[(close + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(fields.Length > 19);
        var birth = ulong.Parse(fields[19], CultureInfo.InvariantCulture); // field22: kernel ticks since boot
        Assert.True(birth > 0);
        return birth;
    }

    private async Task<JointInterfaceNativeOutcome> RunJointInterfaceNativeChildAsync(string role, string root, string output, int cut, string own,
        JointInterfaceCrashOwnedContainer ownership)
    {
        var paths = MortalArchiveDependencyPaths();
        var executable = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet");
        string[] arguments = ["exec", "--runtimeconfig", paths["runtimeconfig"], "--depsfile", paths["depsfile"], paths["support"],
            role, paths["integration"], root, output, cut.ToString(CultureInfo.InvariantCulture), own];
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, WorkingDirectory = own };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["TMPDIR"] = Path.Combine(own, "tmp"); // child-only owned namespace, no global environment mutation
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Actual joint engine child was not started.");
        ownership.Started(child.Id);
        DateTime birth = default;
        ulong birthTick = 0;
        Task<string>? stdout = null, stderr = null;
        Exception? failure = null;
        try
        {
            birth = child.StartTime.ToUniversalTime();
            birthTick = ReadJointInterfaceLinuxBirthTick(child.Id); // live owned child, independent of child-self record
            stdout = child.StandardOutput.ReadToEndAsync(); stderr = child.StandardError.ReadToEndAsync();
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(220));
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            Exception? cleanupFailure = null;
            try
            {
                if (!child.HasExited) child.Kill(entireProcessTree: true); // only this original owned child tree
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                stdout ??= child.StandardOutput.ReadToEndAsync(); stderr ??= child.StandardError.ReadToEndAsync();
                await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
                ownership.Joined(child.Id); // only positive terminal+join+EOF can permit container deletion
            }
            catch (Exception error) { cleanupFailure = error; if (failure is not null) failure.Data["JointInterfaceChildCleanupFailure"] = error; }
            if (stdout?.IsCompletedSuccessfully == true) File.WriteAllText(output + ".stdout", stdout.Result);
            if (stderr?.IsCompletedSuccessfully == true) File.WriteAllText(output + ".stderr", stderr.Result);
            WritePhysicalInjuryDiagnostic("iobs_actual_native_join", new
            { child.Id, BirthUtc = birth, BirthTick = birthTick, ExitCode = child.HasExited ? (int?)child.ExitCode : null, Eof = stdout?.IsCompletedSuccessfully == true && stderr?.IsCompletedSuccessfully == true,
                Joined = child.HasExited, Failure = failure?.ToString(), Executable = executable, Arguments = arguments,
                Dependencies = MortalArchiveDependencyContext(), RawStdout = stdout?.IsCompletedSuccessfully == true ? stdout.Result : null,
                RawStderr = stderr?.IsCompletedSuccessfully == true ? stderr.Result : null, CleanupFailure = cleanupFailure?.ToString(),
                RetainedOwnedContainer = ownership.HasUnsettledChildren ? own : null });
            if (cleanupFailure is not null && failure is null) throw cleanupFailure;
        }
        return new(child.Id, birth, birthTick, child.ExitCode, await stdout!, await stderr!, executable, arguments,
            MortalArchiveDependencyContext(), stdout!.IsCompletedSuccessfully && stderr!.IsCompletedSuccessfully, child.HasExited);
    }

    private sealed class JointInterfaceCrashOwnedContainer(string root, Action<string> output) : IDisposable
    {
        private readonly HashSet<int> _unsettled = [];
        internal bool HasUnsettledChildren => _unsettled.Count != 0;
        internal void Started(int pid) => Assert.True(_unsettled.Add(pid));
        internal void Joined(int pid) => Assert.True(_unsettled.Remove(pid));
        public void Dispose()
        {
            if (HasUnsettledChildren)
            {
                output(JsonSerializer.Serialize(new { CleanupOwnedRoot = root, OwnedFixtureRemoved = false,
                    OwnedFixtureRetainedForUnresolvedChild = true, UnsettledOwnedPids = _unsettled.Order().ToArray(),
                    CleanupFailure = "Native exit/join/EOF could not all be positively established; do not delete under an unconfirmed child." }));
                return;
            }
            new CleanupOwnedFixture(root, output).Dispose();
        }
    }

    public static async Task WriteJointInterfaceOriginalCrashAsync(string ignoredRoot, string output, int cut, string own)
    {
        Assert.InRange(cut, 0, 4);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
        using var fixture = new GameEngineTurnLifecycleTests(new JointInterfaceConsoleOutput());
        var scenario = cut == 2 ? "Crash:PublishedCulture" : cut == 4 ? "Crash:Error" : "Crash:Changed";
        using var probe = new JointInterfaceLifecycleProbe(fixture, scenario);
        probe.ConfigureActualCrash(cut, output, own);
        await fixture.RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
        // An unarmed/missing cut must fail after actual old/new work, never write
        // a synthetic cut file or claim that an exception simulated process loss.
        throw new InvalidOperationException("Actual requested joint publication crash boundary did not execute.");
    }

    public static async Task WriteJointInterfaceCurrentResumeAsync(string root, string output, int cut, string own)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        var parent = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "parent.json")))!.AsObject();
        var atCut = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "cut.json")))!.AsObject();
        var fault = parent["Fault"]?.GetValue<string>();
        Assert.Equal(root, atCut["Root"]!.GetValue<string>()); Assert.Equal(cut, atCut["Cut"]!.GetValue<int>());
        Assert.NotEqual(atCut["Pid"]!.GetValue<int>(), Environment.ProcessId);
        var diagnostic = new JointInterfaceResumeDiagnostic(parent["Diagnostic"]?.GetValue<bool>() == true,
            cut, root, parent["Nonce"]!.GetValue<string>());
        diagnostic.Observe("factory-enter");
        using var factory = new GameEngineTurnLifecycleTests(new JointInterfaceConsoleOutput());
        diagnostic.Observe("factory-return");
        using var auxiliaryOwned = new CleanupOwnedFixture(factory._rootPath, Console.WriteLine);
        var policyRejected = false;
        if (cut == 2)
        {
            var contrast = factory.CreateGameEngine(new QueuedConsoleInputSource([]), InertRealmSettings);
            await using var contrastAudio = GetPrivateField<AudioService>(contrast, "_audioService");
            var raw = new JsonObject { ["dialogueOptions"] = new JsonArray("Продолжить разговор."), ["timestamp"] = "13/02/2026" };
            await factory._fs.WriteFileAtomicAsync(MortalJointInterfacePublication.InterfacePath, raw.ToJsonString());
            var issues = await GetPrivateField<ValidationService>(contrast, "_validator").ValidateAcceptedTurnInterfacePayloadAsync();
            policyRejected = issues.Any(issue => issue.Code == "interface_updates_invalid_timestamp" && issue.Severity == IssueSeverity.Error);
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "iobs_independent_ordinary_policy_contrast", Culture = CultureInfo.CurrentCulture.Name,
                Raw = raw, Issues = issues, SeparateOwnedRoot = factory._rootPath }));
            Assert.True(policyRejected);
        }
        FileSystemManager files = null!;
        var changingI = 0; var actualCaller = false;
        MortalJointInterfacePublication.Record? completedProof = null;
        var publications = new List<object>(); var recovery = new List<object>();
        diagnostic.Observe("file-system-enter");
        files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
        {
            LocalPublicationRecoveryObserver = (phase, index) => recovery.Add(new { Phase = phase.ToString(), Index = index }),
            LocalPublicationObserver = (phase, index) =>
            {
                if (phase != TrustedLocalPublicationPhase.Committed) return;
                using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
                foreach (var member in journal.RootElement.GetProperty("Members").EnumerateArray())
                {
                    if (member.GetProperty("Path").GetString() == files.ResolvePath(MortalJointInterfacePublication.InterfacePath) &&
                        !JointInterfaceLifecycleProbe.ImagesEqual(JointInterfaceLifecycleProbe.Image(member, "Before"), JointInterfaceLifecycleProbe.Image(member, "After"))) changingI++;
                    if (member.GetProperty("Path").GetString() == files.ResolvePath(MortalQteDecisionPublication.StatePath))
                        publications.Add(new { Marker = JointInterfaceLifecycleProbe.Image(member, "After"), Journal = journal.RootElement.Clone() });
                }
            }
        });
        diagnostic.Observe("file-system-return");
        var log = new SpiritualLifecycleTestLogger();
        ILogger<GameEngine> engineLogger = diagnostic.Enabled ? new JointInterfaceResumeDiagnosticLogger(log, diagnostic) : log;
        diagnostic.Observe("engine-enter");
        var engine = factory.CreateGameEngine(new QueuedConsoleInputSource([]), settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            fileSystem: files, logger: engineLogger, finalizationHooks: new GameEngineSessionFinalizationHooks
            { AtCheckpointAsync = async point =>
                {
                    diagnostic.Observe("finalization-enter", point.ToString());
                    if (point == SessionFinalizationCheckpoint.LateTerminalAndIdleOperationBound) actualCaller = true;
                    if (cut < 3 && fault is null && point == SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization)
                    {
                        diagnostic.Observe("completed-proof-lease-enter");
                        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                        diagnostic.Observe("completed-proof-lease-return");
                        var actualOriginal = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
                        completedProof = await MortalQteDecisionPublication.ReadCurrentJointInterfaceDataAsync(files, lease,
                            actualOriginal.SessionId, actualOriginal.RequestId, actualOriginal.SnapshotToken, actualOriginal.TurnNumber);
                        Assert.NotNull(completedProof);
                        AssertActualJointInterfaceRegistrySettled(files);
                        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "iobs_actual_completed_data_before_terminal_cleanup",
                            Original = new { actualOriginal.SessionId, actualOriginal.RequestId, actualOriginal.SnapshotToken, actualOriginal.TurnNumber },
                            Record = completedProof.ToJson(), Interface = await files.ReadFileBytesAsync(lease, MortalJointInterfacePublication.InterfacePath),
                            Meaning = "Full existing current original/history/resource proof while actual original remains present; no post-cleanup authority recreation" }));
                    }
                    diagnostic.Observe("finalization-return", point.ToString());
                } });
        diagnostic.Observe("engine-return");
        await using var audio = GetPrivateField<AudioService>(engine, "_audioService");
        var noFresh = GetPrivateFieldValue<object?>(engine, "_mortalJointRepairCheckpoint") is null &&
            GetPrivateFieldValue<string?>(engine, "_mortalJointInterfaceFreshCheckpoint") is null &&
            GetPrivateFieldValue<object?>(engine, "_mortalJointInterfaceReady") is null;
        Assert.True(noFresh);
        MortalJointInterfacePublication.Record? result = null;
        Dictionary<string, byte[]?>? before = null, after = null, repeated = null;
        byte[]? recoveredPending = null, recoveredI = null;
        bool? handled = null, second = null;
        var integrityGuardEntries = new List<object>();
        const string integrityReason = "joint_interface_actual_publication_preimage_or_readback_changed";
        static int ReasonCount(string logs) => logs.Split(integrityReason, StringSplitOptions.None).Length - 1;
        void RequireCurrentInterfaceGuard(int entry, string logsBefore, string logsAfter)
        {
            if (fault != "CurrentInterface") return;
            var countBefore = ReasonCount(logsBefore); var countAfter = ReasonCount(logsAfter);
            var observed = new { Boundary = "iobs_actual_current_interface_guard_entry", Entry = entry,
                Reason = integrityReason, CountBefore = countBefore, CountAfter = countAfter,
                LogsBefore = logsBefore, LogsAfter = logsAfter };
            integrityGuardEntries.Add(observed);
            Console.WriteLine(JsonSerializer.Serialize(observed)); // retained even when the stronger oracle fails
            Assert.True(countAfter > countBefore, "Each real entry must newly report " + integrityReason);
        }
        Exception? failure = null;
        var completed = false;
        try
        {
            PendingTurnSnapshotReadAuthority original;
            diagnostic.Observe("preflight-lease-enter");
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                diagnostic.Observe("preflight-lease-return");
                // This actual new ordinary lease recovers the original journal
                // BEFORE the original/result proof. No copied root or old Ready.
                diagnostic.Observe("snapshot-enter");
                original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
                diagnostic.Observe("snapshot-return");
                Assert.Equal(43, original.TurnNumber);
                recoveredPending = await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
                var pending = ParseJsonObjectBytes(Assert.IsType<byte[]>(recoveredPending));
                var parsed = MortalJointRepairCheckpoint.Parse(JsonSerializer.SerializeToElement(pending["jointRepairCheckpoint"]));
                Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues)); Assert.True(parsed.Checkpoint!.UsesInterfacePublication);
                if (cut != 3)
                {
                    result = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(pending[MortalJointInterfacePublication.PendingField]));
                    result.RequireCheckpoint(parsed.Checkpoint.ToJsonObject()); result.RequireLegacyPending(pending);
                    var cutRecord = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(atCut["Result"]));
                    Assert.Equal(cutRecord.ResultFingerprint, result.ResultFingerprint);
                    if (cut == 4) Assert.True(JsonNode.DeepEquals(cutRecord.ToJson(), result.ToJson()));
                    else Assert.True(JsonNode.DeepEquals(cutRecord.AsPublished().ToJson(), result.AsPublished().ToJson()));
                    Assert.Equal(cut == 2 ? "Published" : "Prepared", result.State);
                    recoveredI = await files.ReadFileBytesAsync(lease, MortalJointInterfacePublication.InterfacePath);
                    Assert.Equal(cut == 2 ? result.AfterBytes : result.BeforeBytes, recoveredI);
                }
                else Assert.False(pending.ContainsKey(MortalJointInterfacePublication.PendingField));
                diagnostic.Observe("runtime-refresh-enter");
                await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateReadOnlyAsync(lease);
                diagnostic.Observe("runtime-refresh-return");
                Assert.Equal(42, GetPrivateField<StateManager>(engine, "_stateManager").CurrentState.TurnNumber);
                GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(original.SessionId, 42);
                if (fault is not null)
                {
                    Assert.Equal(0, cut); Assert.NotNull(result); Assert.Equal("Prepared", result.State);
                    var damagedPending = ParseJsonObjectBytes(Assert.IsType<byte[]>(recoveredPending));
                    var faultPath = fault switch
                    {
                        "Version" or "RecordFingerprint" => WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                        "OriginalManifest" => LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                        "CurrentInterface" => MortalJointInterfacePublication.InterfacePath,
                        _ => throw new InvalidDataException("Unknown actual resume test fault: " + fault)
                    };
                    var beforeFault = await files.ReadFileBytesAsync(lease, faultPath); Assert.NotNull(beforeFault);
                    byte[] afterFault;
                    if (fault == "Version")
                    { damagedPending["jointRepairCheckpoint"]!["schemaVersion"] = 7; afterFault = JsonSerializer.SerializeToUtf8Bytes(damagedPending); }
                    else if (fault == "RecordFingerprint")
                    { damagedPending[MortalJointInterfacePublication.PendingField]!["fingerprint"] = new string('0', 64); afterFault = JsonSerializer.SerializeToUtf8Bytes(damagedPending); }
                    else afterFault = beforeFault.Concat(new byte[] { 32 }).ToArray();
                    File.WriteAllBytes(files.ResolvePath(faultPath), afterFault); // one partial physical fault, never reseal/reissue original/capability
                    Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "iobs_actual_current_resume_integrity_fault", fault,
                        Path = faultPath, Before = beforeFault, After = afterFault, GenuinePrepared = result.ToJson(),
                        Meaning = "Actual abrupt producer and complete baseline proof precede this fault; only new current engine may classify it" }));
                    if (faultPath == WoundAcceptedTurnSnapshotContract.PendingResolutionPath) recoveredPending = afterFault;
                }
                diagnostic.Observe("before-images-enter");
                before = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                diagnostic.Observe("before-images-return");
            }
            diagnostic.Observe("preflight-lease-release-return");
            var firstLogsBefore = log.Describe();
            diagnostic.Observe("late-first-enter");
            handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            diagnostic.Observe("late-first-return", handled.ToString());
            var firstLogsAfter = log.Describe();
            RequireCurrentInterfaceGuard(1, firstLogsBefore, firstLogsAfter);
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                diagnostic.Observe("after-images-enter");
                after = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                diagnostic.Observe("after-images-return");
                if (cut < 3 && fault is null)
                {
                    var current = completedProof; // physical proof frozen at the actual pre-cleanup checkpoint
                    Assert.NotNull(current); Assert.Equal("Published", current.State); Assert.False(current.HasErrors);
                    var cutRecord = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(atCut["Result"]));
                    Assert.Equal(cutRecord.ResultFingerprint, current.ResultFingerprint);
                    Assert.True(JsonNode.DeepEquals(cutRecord.AsPublished().ToJson(), current.ToJson()));
                    Assert.Equal(cutRecord.AfterBytes, await files.ReadFileBytesAsync(lease, MortalJointInterfacePublication.InterfacePath));
                    var image = ParseJsonObjectBytes(Assert.IsType<byte[]>(cutRecord.AfterBytes));
                    var option = Assert.IsType<JsonObject>(Assert.Single(image["dialogueOptions"]!.AsArray()));
                    Assert.Equal("[NI_CONTROL:second]Продолжить обычный ответ.", option["text"]!.GetValue<string>());
                    Assert.Equal("[NI_ACTION:first][NI_CONTROL:second]Продолжить обычный ответ.", option["inputValue"]!.GetValue<string>());
                    Assert.Equal(43, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
                    await AssertJointInterfaceResumedMechanicalOutcomeAsync(files, lease, before, current.Binding);
                    foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                        MortalWoundOpportunityAdapter.DecisionPath, "input/turn_request.json", "ready/turn_complete.json",
                        LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath })
                        Assert.Null(await files.ReadFileBytesAsync(lease, path));
                    result = current; completed = true;
                }
                else
                {
                    Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
                    Assert.Equal(recoveredPending, await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
                    Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
                    foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
                    if (cut == 4 && result is not null)
                    {
                        Assert.True(result.HasErrors);
                        foreach (var issue in result.Issues) Assert.Contains(JsonSerializer.Serialize(issue), log.Describe());
                    }
                }
            }
            var secondLogsBefore = log.Describe();
            diagnostic.Observe("late-second-enter");
            second = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            diagnostic.Observe("late-second-return", second.ToString());
            var secondLogsAfter = log.Describe();
            RequireCurrentInterfaceGuard(2, secondLogsBefore, secondLogsAfter);
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                diagnostic.Observe("repeat-images-enter");
                repeated = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                diagnostic.Observe("repeat-images-return");
                await AssertConfirmedClaimReadImagesAsync(files, lease, after);
            }
            Assert.Equal(fault is null && cut < 2 ? 1 : 0, changingI); Assert.True(actualCaller);
            if (completed) Assert.False(second!.Value); else Assert.True(GetPrivateFieldValue<bool>(engine, "_mortalJointInterfacePreparationHeld"));
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            diagnostic.Observe("resume-finally", failure?.GetType().FullName);
            File.WriteAllText(output, JsonSerializer.Serialize(new
            { Pid = Environment.ProcessId, BirthUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                BirthTick = ReadJointInterfaceLinuxBirthTick(Environment.ProcessId), Nonce = parent["Nonce"]!.GetValue<string>(),
                Root = root, AuxiliaryRoot = factory._rootPath, ActualCallerEntered = actualCaller, Handled = handled, Second = second,
                Fault = fault, Completed = completed, Held = !completed && failure is null, Failure = failure?.ToString(),
                NewChangingICommits = changingI, InertRepeatBytesEqual = repeated is not null && after is not null &&
                    repeated.Count == after.Count && repeated.All(row => after.TryGetValue(row.Key, out var bytes) && MortalJointInterfacePublication.BytesEqual(row.Value, bytes)),
                FreshAuthorityFieldsAbsentBeforeEntry = noFresh, AmbientParseAccepted = DateTimeOffset.TryParse("13/02/2026", out _),
                IndependentOrdinaryPolicyRejected = policyRejected, Culture = CultureInfo.CurrentCulture.Name,
                Result = result?.ToJson(), RecoveredPending = recoveredPending, RecoveredI = recoveredI,
                Before = before, After = after, Repeated = repeated, Publications = publications, Recovery = recovery, Logs = log.Describe(),
                IntegrityGuardEntries = integrityGuardEntries, DiagnosticOutputFailure = diagnostic.OutputFailure,
                Dependencies = MortalArchiveDependencyContext(), Loaded = MortalArchiveLoadedDependencyBindings() }));
        }
    }

    private static async Task AssertJointInterfaceResumedMechanicalOutcomeAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, IReadOnlyDictionary<string, byte[]?> before, JsonObject binding)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath), false);
        Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
        var history = ResourceHistoryState.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
        Assert.True(state.IsValid, DescribeValidationIssues(state.Issues)); Assert.True(history.IsValid, DescribeValidationIssues(history.Issues));
        Assert.Empty(history.History!.ValidateStateAgreement(state.Ledger!));
        var spend = Assert.Single(history.History.Transitions, row => row.Turn == 43 && row.Coordinate.ResourceKey == "energy");
        Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation); Assert.Equal(ResourceMutationPhase.RegisteredSystemOutcome, spend.Phase);
        Assert.Equal(2m, spend.RequestedAmount); Assert.Equal(2m, spend.AppliedAmount); Assert.Equal(2m, spend.BeforeState!.Current); Assert.Equal(0m, spend.AfterState!.Current);
        Assert.Equal(0m, Assert.Single(state.Ledger!.Entries, row => row.Coordinate.ResourceKey == "energy").Current);
        var damage = Assert.Single(history.History.Transitions, row => row.Turn == 43 && row.Coordinate.ResourceKey == "health");
        Assert.Equal(ResourceTransitionOperation.Damage, damage.Operation); Assert.Equal(ResourceMutationPhase.EffectTrigger, damage.Phase);
        Assert.Equal(3m, damage.AppliedAmount); Assert.Equal(5m, damage.BeforeState!.Current); Assert.Equal(2m, damage.AfterState!.Current);
        Assert.Equal(2m, Assert.Single(state.Ledger.Entries, row => row.Coordinate.ResourceKey == "health").Current);
        Assert.Equal(2, history.History.Transitions.Count(row => row.Turn == 43));
        var prior = WoundHistoryState.Parse(MortalArchiveText(before[WoundHistoryState.HistoryPath]!), WoundHistoryState.HistoryPath);
        var current = WoundHistoryState.Parse(await files.ReadFileAsync(lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        Assert.True(prior.IsValid, DescribeValidationIssues(prior.Issues)); Assert.True(current.IsValid, DescribeValidationIssues(current.Issues));
        foreach (var row in prior.State!.Transitions)
            Assert.Equal(JsonSerializer.Serialize(row, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                JsonSerializer.Serialize(Assert.Single(current.State!.Transitions, now => now.Ordinal == row.Ordinal), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var suffix = current.State!.Transitions.Skip(prior.State.Transitions.Count).ToArray();
        Assert.Equal(2, suffix.Length); Assert.Equal("create", suffix[0].Kind); Assert.Equal("treat", suffix[1].Kind);
        Assert.Equal(prior.State.NextOrdinal, suffix[0].Ordinal); Assert.Equal(prior.State.NextOrdinal + 1, suffix[1].Ordinal);
        Assert.All(suffix, row => Assert.Equal(43, row.Turn));
        var treatment = Assert.IsType<MortalWoundTreatmentPersistedResult>(suffix[1].TreatmentResult);
        Assert.Equal(binding["requestFingerprint"]!.GetValue<string>(), treatment.Request.RequestFingerprint);
        Assert.Equal(suffix[1].WoundId, treatment.Request.Coordinates.WoundId);
        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "iobs_actual_resumed_once_mechanics", spend, damage,
            OriginalWoundPrefix = prior.State.Transitions, ExactSuffix = suffix, RequestFingerprint = treatment.Request.RequestFingerprint,
            Meaning = "Independent 2→0 charge, 5→2 damage, exactly two target-turn resource rows and create/treat suffix; full current proof was frozen before cleanup" }));
    }

    private static void AssertActualJointInterfaceRegistrySettled(FileSystemManager files)
    {
        // Observe the slot created by THIS real current common publisher. This
        // diagnostic lookup neither initializes a state nor supplies capability.
        var roots = typeof(AcceptedTurnAuthorityRegistry).GetField("RootSlots", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object?[] arguments = [files.CanonicalRootAuthorityIdentity, null];
        Assert.True((bool)roots.GetType().GetMethod("TryGetValue")!.Invoke(roots, arguments)!);
        var state = GetPrivateField<object>(arguments[1]!, "_state");
        Assert.Null(GetPrivateFieldValue<object?>(state, "_mortalWoundTreatmentPublicationReservation"));
        Assert.Null(GetPrivateFieldValue<object?>(state, "_openJointRepairPublicationReceipt"));
        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "iobs_actual_current_common_claim_settled",
            Root = files.BasePath, ActualState = state.GetType().FullName, ReservationAbsent = true, OpenTakeAbsent = true,
            Meaning = "Actual completed physical proof precedes this read of existing owner state, not a fabricated receipt or old-process claim" }));
    }

    private sealed partial class JointInterfaceLifecycleProbe
    {
        private int? _actualCrashCut;
        private string? _crashOutput, _crashOwn;
        internal void ConfigureActualCrash(int cut, string output, string own)
        { Assert.Null(_actualCrashCut); _actualCrashCut = cut; _crashOutput = output; _crashOwn = own; }

        private void ObserveActualCrashCut(TrustedLocalPublicationPhase phase, int index)
        {
            if (_actualCrashCut is not { } cut || (cut != 3 && !_configured) ||
                phase is not (TrustedLocalPublicationPhase.Committed or TrustedLocalPublicationPhase.MemberPublished)) return;
            var journalBytes = File.ReadAllBytes(Path.Combine(fixture._fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json"));
            using var journal = CleanupPublicationCut.Metadata(journalBytes);
            var members = journal.RootElement.GetProperty("Members").EnumerateArray().ToArray();
            var member = members.FirstOrDefault(row => row.GetProperty("Path").GetString() == fixture._fs.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
            if (member.ValueKind == JsonValueKind.Undefined || Image(member, "After") is not { } pendingBytes) return;
            var pending = ParseJsonObjectBytes(pendingBytes);
            if (pending["jointRepairCheckpoint"] is not JsonObject checkpoint || checkpoint["schemaVersion"]?.GetValue<int>() != 2) return;
            var record = pending[MortalJointInterfacePublication.PendingField] is { } value
                ? MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(value)) : null;
            var reached = cut switch
            {
                0 => phase == TrustedLocalPublicationPhase.Committed && record is { State: "Prepared", HasErrors: false },
                1 => phase == TrustedLocalPublicationPhase.MemberPublished && record is { State: "Published", HasErrors: false } &&
                    members[index].GetProperty("Path").GetString() == fixture._fs.ResolvePath(InterfacePath),
                2 => phase == TrustedLocalPublicationPhase.Committed && record is { State: "Published", HasErrors: false },
                3 => phase == TrustedLocalPublicationPhase.Committed && record is null,
                4 => phase == TrustedLocalPublicationPhase.Committed && record is { State: "Prepared", HasErrors: true },
                _ => false
            };
            if (!reached) return;
            var parent = JsonNode.Parse(File.ReadAllText(Path.Combine(_crashOwn!, "parent.json")))!;
            Assert.Equal(0, _captureCallbacks); Assert.Null(_firstMarker);
            if (cut == 1) Assert.False(journal.RootElement.GetProperty("Committed").GetBoolean());
            else Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
            foreach (var (path, bytes) in _mechanicalBefore) Assert.Equal(bytes, ReadPhysical(path));
            File.WriteAllText(_crashOutput!, JsonSerializer.Serialize(new
            { Pid = Environment.ProcessId, BirthUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                BirthTick = ReadJointInterfaceLinuxBirthTick(Environment.ProcessId), Nonce = parent["Nonce"]!.GetValue<string>(),
                ParentPid = parent["ParentPid"]!.GetValue<int>(), Root = fixture._fs.BasePath, Cut = cut,
                Phase = phase.ToString(), Index = index, JournalBase64 = Convert.ToBase64String(journalBytes),
                ActualPending = ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath), PendingMemberAfter = pendingBytes,
                Interface = ReadPhysical(InterfacePath), InterfaceMtime = Mtime(), Result = record?.ToJson(), Checkpoint = checkpoint,
                CommonCallbacks = _captureCallbacks, FirstMarker = _firstMarker, Turn = GetPrivateField<GameLoop>(_engine!, "_gameLoop").TurnNumber,
                MechanicalBefore = _mechanicalBefore, MechanicalAtCut = _mechanicalBefore.Keys.ToDictionary(path => path, ReadPhysical),
                AmbientParseAccepted = DateTimeOffset.TryParse("13/02/2026", out _), Culture = CultureInfo.CurrentCulture.Name,
                Dependencies = MortalArchiveDependencyContext(), Loaded = MortalArchiveLoadedDependencyBindings() }));
            Console.Out.Flush(); Console.Error.Flush();
            Environment.Exit(73); // actual abrupt process exit; no catch, finally, release or compensation
        }
    }

    private sealed class JointInterfaceConsoleOutput : ITestOutputHelper
    {
        public void WriteLine(string message) => Console.WriteLine(message);
        public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
    }
}
