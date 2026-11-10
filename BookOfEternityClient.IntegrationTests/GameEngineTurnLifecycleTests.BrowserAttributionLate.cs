using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task BrowserAdmissionDiagnostics_LateTerminalBranchCapturesOnceAndPreservesOriginalFailure()
    {
        var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
        var condition = GameEngine.ValidateDetachedBrowserBinding(staged).BrowserOriginalMainCondition;
        Assert.NotNull(condition);
        Assert.Equal("quiescentAbsent", condition.Kind);
        Assert.Equal(staged.Binding.Generation, condition.Generation);
        Assert.Null(condition.ActiveIdentity);
        var original = new IOException("original late-terminal checkpoint failure");
        var reached = 0;
        var engine = CreateGameEngine(finalizationHooks: new GameEngineSessionFinalizationHooks
        {
            AtCheckpointAsync = checkpoint => checkpoint == SessionFinalizationCheckpoint.LateTerminalAndIdleOperationBound
                ? FailOriginal() : Task.CompletedTask
        });
        Task FailOriginal() { reached++; return Task.FromException(original); }
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession("browser-recovery-session", 0);
        var type = typeof(FileSystemManager).Assembly.GetType("BookOfEternityClient.Core.BrowserAdmissionDiagnosticCollector", true)!;
        var collector = type.GetMethod("CreateForTests", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { (Func<long>)System.Diagnostics.Stopwatch.GetTimestamp, 4096 })!;
        typeof(FileSystemManager).GetField("_browserAdmissionDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(_fs, collector);
        JsonNode Snapshot() => JsonNode.Parse((string)type.GetMethod("SnapshotJson", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(collector, null)!)!;

        // The actual staged idle return must not create a terminal window.
        Assert.True(await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
        Assert.Equal(0, reached);
        Assert.Empty(Snapshot()["Windows"]!.AsArray());
        var request = JsonSerializer.Deserialize<TurnRequest>(staged.RequestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", JsonSerializer.Serialize(new
        {
            sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
            timestamp = DateTime.UtcNow.ToString("O"), status = "success", filesModified = Array.Empty<string>()
        }));
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine,
            "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
        Assert.Same(original, failure);
        Assert.Equal(1, reached);
        var result = Snapshot();
        _directGachaOutput?.WriteLine("actual-late-branch-profile=" + result.ToJsonString());
        var window = Assert.Single(result["Windows"]!.AsArray())!;
        Assert.Equal(staged.Binding.ActionId, window["ActionId"]!.GetValue<string>());
        Assert.Equal(staged.Binding.Generation, window["Generation"]!.GetValue<string>());
        Assert.Equal("unbound", window["RequestId"]!.GetValue<string>()); // Original snapshot load is not reached.
        Assert.Null(window["OwnerIdentity"]); // Guarded quiescent condition has no active owner identity.
        Assert.Equal("failed", window["Outcome"]!.GetValue<string>());
        Assert.Null(window["AcceptedTicks"]);
        Assert.NotNull(window["ClosedTicks"]);
        var markers = window["Markers"]!.AsObject();
        Assert.True(markers.ContainsKey("late-terminal-observed"));
        Assert.False(markers.ContainsKey("terminal-wait-returned"));
        Assert.True(markers.ContainsKey("processing-owner"));
        Assert.True(markers.ContainsKey("finalizer-failed"));
        Assert.True(result["Complete"]!.GetValue<bool>()); // Complete failure capture, never accepted success.
    }

    [Fact]
    public async Task BrowserAdmissionDiagnostics_MetaSubgroupsPreserveRealReadOrderIssuesAndFailure()
    {
        const string failurePath = "game_state/meta/achievements.json";
        const string manifestationPath = "game_state/control/pending_resident_companion_manifestation_request.json";
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var type = typeof(FileSystemManager).Assembly.GetType(
                "BookOfEternityClient.Core.BrowserAdmissionDiagnosticCollector", true)!;
            var collectorField = typeof(FileSystemManager).GetField("_browserAdmissionDiagnostic",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var previousCollector = collectorField.GetValue(probe.Files);
            object NewCollector() => type.GetMethod("CreateForTests", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { (Func<long>)System.Diagnostics.Stopwatch.GetTimestamp, 4096 })!;
            JsonNode Capture(object collector) => JsonNode.Parse((string)type.GetMethod("SnapshotJson",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(collector, null)!)!;
            IDisposable Parent(object collector) => (IDisposable)type.GetMethod("Logical",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(collector,
                new object[] { "meta-subgroup-test-parent", "meta-subgroup-test-caller" })!;
            string ReadPhase(object? collector)
            {
                if (collector == null) return "disabled";
                // Observe the existing immutable logical tag at the real after-read hook.
                // No owner/phase/lease is manufactured and no product stack walk or I/O is added.
                var local = type.GetField("_logical", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(collector)!;
                var tag = local.GetType().GetProperty("Value")!.GetValue(local);
                if (tag == null) return "unbound";
                var labels = tag.GetType().GetProperty("Labels")!.GetValue(tag)!;
                return (string)labels.GetType().GetProperty("ValidationPhase")!.GetValue(labels)!;
            }
            object[] TraceRows(List<(string Path, string Phase)> trace) => trace
                .Select(row => (object)new { row.Path, row.Phase }).ToArray();
            async Task<(string Issues, string[] Opens, List<(string Path, string Phase)> Trace)> ReadMeta(object? collector)
            {
                collectorField.SetValue(probe.Files, collector);
                var trace = new List<(string Path, string Phase)>();
                probe.OnAfterRead = path =>
                {
                    trace.Add((path, ReadPhase(collector)));
                    return Task.CompletedTask;
                };
                probe.Arm();
                using var parent = collector == null ? null : Parent(collector);
                var issues = await probe.Validator.ValidateGameStateAsync(GameStateValidationPhase.MetaMiscStateFiles);
                return (WorldIssues(issues), probe.OpenedReads.ToArray(), trace);
            }
            var inputs = WorldInputImages(probe.Files);
            var primary = new IOException("original real Meta subgroup read failure");
            async Task<(Exception? Failure, bool ResultPublished, string[] Opens, List<(string Path, string Phase)> Trace,
                string? Stack, JsonNode? Profile)> ReadFailure(object? collector)
            {
                collectorField.SetValue(probe.Files, collector);
                var trace = new List<(string Path, string Phase)>();
                var reached = 0;
                probe.OnAfterRead = path =>
                {
                    trace.Add((path, ReadPhase(collector)));
                    return Task.CompletedTask;
                };
                probe.OnRead = path => path == failurePath && ++reached == 1
                    ? Task.FromException(primary) : Task.CompletedTask;
                probe.Arm();
                using var parent = collector == null ? null : Parent(collector);
                List<ValidationIssue>? result = null;
                var failure = await Record.ExceptionAsync(async () =>
                {
                    result = await probe.Validator.ValidateGameStateAsync(GameStateValidationPhase.MetaMiscStateFiles);
                });
                var opens = probe.OpenedReads.ToArray();
                var stack = failure?.StackTrace; // Freeze before the same exception is thrown by the second real invocation.
                probe.OnRead = null;
                probe.OnAfterRead = null;
                // Real post-failure read under the original caller scope witnesses restoration.
                if (collector != null)
                {
                    Assert.Equal("meta-subgroup-test-parent", ReadPhase(collector));
                    Assert.Equal("17", await probe.Files.ReadFileAsync(failurePath));
                }
                Assert.Equal(1, reached);
                return (failure, result != null, opens, trace, stack, collector == null ? null : Capture(collector));
            }
            try
            {
                // This actual fixture member reaches the unchanged realm-context read before its realm decision.
                Assert.Equal("{}", await probe.Files.ReadFileAsync(manifestationPath));
                var disabled = await ReadMeta(null);
                var disabledInputs = WorldInputImages(probe.Files);
                var collector = NewCollector();
                var enabled = await ReadMeta(collector);
                var enabledInputs = WorldInputImages(probe.Files);
                var profile = Capture(collector);
                var disabledFailure = await ReadFailure(null);
                var failedCollector = NewCollector();
                var enabledFailure = await ReadFailure(failedCollector);
                WorldEvidence(probe, new
                {
                    MetaSubgroupAttribution = true, Inputs = inputs, DisabledInputs = disabledInputs, EnabledInputs = enabledInputs,
                    disabled.Issues, EnabledIssues = enabled.Issues, DisabledOpens = disabled.Opens, EnabledOpens = enabled.Opens,
                    DisabledReads = TraceRows(disabled.Trace), EnabledReads = TraceRows(enabled.Trace), Profile = profile,
                    DisabledFailureReads = TraceRows(disabledFailure.Trace), EnabledFailureReads = TraceRows(enabledFailure.Trace),
                    DisabledFailure = disabledFailure.Stack, EnabledFailure = enabledFailure.Stack,
                    SameDisabledPrimary = ReferenceEquals(primary, disabledFailure.Failure),
                    SameEnabledPrimary = ReferenceEquals(primary, enabledFailure.Failure), FailureProfile = enabledFailure.Profile
                });
                Assert.Equal(inputs, disabledInputs);
                Assert.Equal(inputs, enabledInputs);
                Assert.Equal(inputs, WorldInputImages(probe.Files));
                Assert.NotEqual("[]", disabled.Issues);
                Assert.Equal(disabled.Issues, enabled.Issues);
                Assert.Equal(disabled.Opens, enabled.Opens);
                Assert.Equal(disabled.Trace.Select(row => row.Path), enabled.Trace.Select(row => row.Path));
                Assert.Same(primary, disabledFailure.Failure);
                Assert.Same(primary, enabledFailure.Failure);
                Assert.False(disabledFailure.ResultPublished);
                Assert.False(enabledFailure.ResultPublished);
                Assert.Contains("ReadOrdinaryFileSnapshotCoreAsync", disabledFailure.Stack);
                Assert.Contains("ReadOrdinaryFileSnapshotCoreAsync", enabledFailure.Stack);
                Assert.Equal(disabledFailure.Opens, enabledFailure.Opens);
                Assert.Equal(disabledFailure.Trace.Select(row => row.Path), enabledFailure.Trace.Select(row => row.Path));
                Assert.True(profile["Complete"]!.GetValue<bool>());
                Assert.True(enabledFailure.Profile!["Complete"]!.GetValue<bool>());
                Assert.Contains(enabledFailure.Profile["ContentRows"]!.AsArray(), row =>
                    row!["Labels"]!["ValidationPhase"]!.GetValue<string>() == "meta-subgroup-test-parent" &&
                    row["Labels"]!["EngineCaller"]!.GetValue<string>() == "meta-subgroup-test-caller");
                Assert.All(profile["ContentRows"]!.AsArray(), row =>
                    Assert.Equal("meta-subgroup-test-caller", row!["Labels"]!["EngineCaller"]!.GetValue<string>()));
                Assert.False(WorldHasAmbientLease(probe.Files));
                // LAST: old source must fail missing groups only after real semantic/fault/restoration evidence.
                // These are observed after-read labels, never source-count or manufactured accepted-window coverage.
                foreach (var group in new[] { "AfterlifeAndSarefShapes", "GuardianAndShiningShapes", "BehaviourAndJournalShapes",
                    "MiscShapes", "ManifestationContext", "SocialRequestShapes", "LifecycleContexts" })
                    Assert.Contains(enabled.Trace, row => row.Phase == "MetaMiscStateFiles/" + group);
            }
            finally
            {
                probe.OnRead = null;
                probe.OnAfterRead = null;
                collectorField.SetValue(probe.Files, previousCollector);
            }
        }, additionalInputs: new Dictionary<string, string>
        {
            [failurePath] = "17", // Nonempty ordered shape issues, not an empty-output-only comparison.
            [manifestationPath] = "{}" // Existing malformed-shape/domain semantics, no lifecycle/action injection.
        });
    }

}
