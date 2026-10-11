using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const string WorldQuestHistory = "game_state/quests/quest_history.json";
    private const string WorldFactionCore = "game_state/factions/faction_core.json";
    private const string WorldFactionChronicles = "game_state/factions/faction_chronicles.json";
    private const string WorldFactionStructure = "game_state/factions/faction_structure.json";
    private static readonly GameStateValidationSelection WorldSelection =
        new(GameStateValidationPhase.WorldQuestCombatFactionStateFiles);

    [Theory]
    [InlineData("absent")]
    [InlineData("mixed")]
    public async Task WorldReadPhase_WholeWindowProofCeilingPreservesOrderedIssues(string layout)
    {
        await WithWorldReadFixture(layout, async probe =>
        {
            // Scalar authority path, same actual original owner and immutable inputs.
            // No override means this reference is ineligible for the proposed pilot.
            if (layout == "absent") Assert.All(WorldCurrentPaths, path => Assert.False(File.Exists(probe.Files.ResolvePath(path))));
            var before = WorldInputImages(probe.Files);
            var scalar = await probe.Validator.ValidateGameStateAsync(WorldSelection);
            Assert.Equal(before, WorldInputImages(probe.Files));
            if (layout == "absent") Assert.Empty(scalar);
            else
            {
                Assert.Equal(new[] { "flexible_state_invalid_root", "strict_state_invalid_root" },
                    scalar.Where(issue => issue.FilePath == WorldQuestHistory).Select(issue => issue.Code));
                Assert.NotEmpty(scalar);
            }

            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            probe.Arm(); // Entire public invocation: opening, callbacks, final gate and close.
            List<ValidationIssue>? actual = null;
            Exception? failure = null;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { actual = await probe.Validator.ValidateGameStateAsync(WorldSelection); }
            catch (Exception error) { failure = error; throw; }
            finally { WorldEvidence(probe, new { layout, before, InvocationSeconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds,
                ScalarIssues = WorldIssues(scalar),
                ActualIssues = actual == null ? null : WorldIssues(actual), Failure = failure?.ToString() }); }
            Assert.Equal(before, WorldInputImages(probe.Files));
            Assert.Equal(WorldIssues(scalar), WorldIssues(actual!));
            Assert.False(WorldHasAmbientLease(probe.Files));
            Assert.InRange(probe.PhysicalProofSpans, 1, 6);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WorldReadPhase_NestedCurrentCoreDriftNeverPublishesIssues(bool coreSelectedOut)
    {
        await WithWorldReadFixture("faction", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var corePath = probe.Files.ResolvePath(WorldFactionCore);
            var before = File.ReadAllBytes(corePath);
            var structureReads = 0;
            probe.OnRead = path =>
            {
                if (path == WorldFactionStructure && ++structureReads == 2)
                    File.WriteAllText(corePath, "{\"factions\":[{\"factionId\":\"faction_changed\",\"name\":\"Changed\"}]}");
                return Task.CompletedTask;
            };
            probe.Arm();
            Exception? failure;
            try
            {
                var selected = coreSelectedOut ? new GameStateValidationSelection(
                    GameStateValidationPhase.WorldQuestCombatFactionStateFiles, [WorldFactionStructure]) : WorldSelection;
                failure = await Record.ExceptionAsync(() => probe.Validator.ValidateGameStateAsync(selected));
                WorldEvidence(probe, new { coreSelectedOut, structureReads, Failure = failure?.ToString(),
                    OriginalCore = Convert.ToHexString(SHA256.HashData(before)),
                    ChangedCore = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(corePath))) });
            }
            finally { probe.OnRead = null; File.WriteAllBytes(corePath, before); }
            Assert.Equal(2, structureReads);
            Assert.NotNull(failure);
            Assert.IsAssignableFrom<IOException>(failure);
            Assert.False(WorldHasAmbientLease(probe.Files));
        });
    }

    [Theory]
    [InlineData("read")]
    [InlineData("cancellation")]
    public async Task WorldReadPhase_ReadFailureKeepsOriginalExceptionAndClearsContext(string kind)
    {
        await WithWorldReadFixture("mixed", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            Exception primary = kind == "read" ? new IOException("owned World read cut") :
                new OperationCanceledException("owned World cancellation cut", new CancellationToken(true));
            var priorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "prior_id" };
            var priorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Prior name" };
            SeedWorldOpeningCaches(probe, priorIds, priorNames);
            probe.OnRead = path => path == WorldQuestHistory ? Task.FromException(primary) : Task.CompletedTask;
            probe.Arm();
            var failure = await Record.ExceptionAsync(() => probe.Validator.ValidateGameStateAsync(WorldSelection));
            probe.OnRead = null;
            WorldEvidence(probe, new { kind, Failure = failure?.ToString(), SamePrimary = ReferenceEquals(primary, failure) });
            Assert.Same(primary, failure);
            Assert.Contains("ReadOrdinaryFileSnapshotCoreAsync", failure!.StackTrace);
            Assert.False(WorldHasAmbientLease(probe.Files));
            Assert.True(probe.SeededOpenings > 0);
            Assert.Same(priorIds, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionIdsCache"));
            Assert.Same(priorNames, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionNamesCache"));
            Assert.Equal(new[] { "prior_id" }, priorIds);
            Assert.Equal(new[] { "Prior name" }, priorNames);
            probe.OnOpened = null;
            // A real fresh invocation after the failure must not inherit the closed reservation.
            await probe.Validator.ValidateGameStateAsync(WorldSelection);
        });
    }

    [Theory]
    [InlineData("owner-close")]
    [InlineData("generation")]
    [InlineData("override-withdraw")]
    public async Task WorldReadPhase_LateOriginalContinuityLossRefusesResult(string kind)
    {
        await WithWorldReadFixture("mixed", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var generationBytes = File.ReadAllBytes(probe.Files.SessionGenerationPath);
            var read = 0;
            var proofSpansAtClose = 0;
            probe.OnRead = path =>
            {
                var target = kind == "owner-close" ? WorldFactionChronicles : WorldQuestHistory;
                if (path == target && ++read == (kind == "owner-close" ? 2 : 1))
                {
                    // Last callback has computed. Its second physical open is the final actual-readset comparison,
                    // ordered after the fresh full end proof and before owning-phase close/result flush.
                    if (kind == "owner-close") { proofSpansAtClose = probe.PhysicalProofSpans; probe.Original.Dispose(); }
                    else if (kind == "override-withdraw") snapshot.Dispose();
                    else
                    {
                        var changed = JsonNode.Parse(generationBytes)!;
                        changed["GenerationId"] = Guid.NewGuid().ToString("N");
                        File.WriteAllText(probe.Files.SessionGenerationPath, changed.ToJsonString());
                    }
                }
                return Task.CompletedTask;
            };
            probe.Arm();
            Exception? failure;
            try
            {
                failure = await Record.ExceptionAsync(() => probe.Validator.ValidateGameStateAsync(WorldSelection));
                WorldEvidence(probe, new { kind, read, proofSpansAtClose, Failure = failure?.ToString() });
            }
            finally { probe.OnRead = null; File.WriteAllBytes(probe.Files.SessionGenerationPath, generationBytes); }
            if (kind == "owner-close") { Assert.Equal(2, read); Assert.True(proofSpansAtClose > 0); }
            else Assert.True(read > 0);
            Assert.NotNull(failure);
            Assert.False(WorldHasAmbientLease(probe.Files));
        }, allowEstablishedReplacement: kind == "generation");
    }

    [Theory]
    [InlineData("already-running-scalar")]
    [InlineData("new-game-state")]
    [InlineData("new-response")]
    [InlineData("override-install")]
    [InlineData("override-dispose")]
    public async Task WorldReadPhase_SameInstanceSiblingAndOverrideExcludedBeforeSharedMutation(string kind)
    {
        await WithWorldReadFixture("mixed", async probe =>
        {
            using var snapshot = kind == "already-running-scalar" ? null :
                probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var pauses = 0;
            probe.OnRead = path => path == WorldQuestHistory && Interlocked.Increment(ref pauses) == 1
                ? Pause() : Task.CompletedTask;
            async Task Pause() { entered.TrySetResult(); await release.Task; }
            probe.Arm();
            var admitted = probe.Validator.ValidateGameStateAsync(WorldSelection);
            Exception? rejected = null;
            Exception? admittedFailure = null;
            IDisposable? laterSnapshot = null;
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var priorIds = GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionIdsCache");
                var priorNames = GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionNamesCache");
                var priorOverride = GetPrivateFieldValue<object?>(probe.Validator, "_prevalidatedPendingTurnSnapshotOverride");
                if (kind == "already-running-scalar")
                {
                    laterSnapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
                    rejected = await Record.ExceptionAsync(() => probe.Validator.ValidateGameStateAsync(WorldSelection));
                    laterSnapshot.Dispose(); laterSnapshot = null;
                }
                else if (kind == "new-game-state")
                    rejected = await Record.ExceptionAsync(() => probe.Validator.ValidateGameStateAsync(GameStateValidationPhase.RequiredFiles));
                else if (kind == "new-response")
                {
                    using var response = JsonDocument.Parse("{\"response\":\"owned response\",\"factions\":[]}");
                    rejected = Record.Exception(() => probe.Validator.ValidateResponse(response.RootElement));
                }
                else if (kind == "override-install")
                    rejected = Record.Exception(() => laterSnapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(null));
                else rejected = Record.Exception(() => snapshot!.Dispose());
                WorldEvidence(probe, new { kind, Rejected = rejected?.ToString(), AdmittedStillPending = !admitted.IsCompleted,
                    SameIds = ReferenceEquals(priorIds, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionIdsCache")),
                    SameNames = ReferenceEquals(priorNames, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionNamesCache")),
                    SameOverride = ReferenceEquals(priorOverride, GetPrivateFieldValue<object?>(probe.Validator, "_prevalidatedPendingTurnSnapshotOverride")) });
                Assert.IsType<InvalidOperationException>(rejected);
                Assert.False(admitted.IsCompleted);
                Assert.Same(priorIds, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionIdsCache"));
                Assert.Same(priorNames, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionNamesCache"));
                Assert.Same(priorOverride, GetPrivateFieldValue<object?>(probe.Validator, "_prevalidatedPendingTurnSnapshotOverride"));
            }
            finally
            {
                probe.OnRead = null;
                laterSnapshot?.Dispose();
                release.TrySetResult();
                admittedFailure = await Record.ExceptionAsync(() => admitted.WaitAsync(TimeSpan.FromSeconds(10)));
                WorldEvidence(probe, new { kind, AdmittedFailure = admittedFailure?.ToString(),
                    AdmittedReturnedIssues = admitted.IsCompletedSuccessfully });
            }
            if (kind == "override-dispose") Assert.NotNull(admittedFailure);
            else Assert.Null(admittedFailure);
            Assert.False(WorldHasAmbientLease(probe.Files));
        });
    }

    [Fact]
    public async Task WorldReadPhase_FreshCacheInputsDoNotEscapePhase()
    {
        await WithWorldReadFixture("faction", async probe =>
        {
            var staleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "stale_id" };
            var staleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Stale name" };
            SeedWorldOpeningCaches(probe, staleIds, staleNames);
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            probe.Arm();
            var issues = await probe.Validator.ValidateGameStateAsync(WorldSelection);
            WorldEvidence(probe, new { Issues = WorldIssues(issues),
                IdCacheRestored = ReferenceEquals(staleIds, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionIdsCache")),
                NameCacheRestored = ReferenceEquals(staleNames, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionNamesCache")) });
            Assert.DoesNotContain(issues, issue => issue.Code == "canonical_faction_sidecar_unknown_faction_id");
            Assert.Equal(new[] { "stale_id" }, staleIds);
            Assert.Equal(new[] { "Stale name" }, staleNames);
            Assert.True(probe.SeededOpenings > 0);
            // Seeded at the existing first physical-open boundary after normal resets, before phase isolation.
            Assert.Same(staleIds, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionIdsCache"));
            Assert.Same(staleNames, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionNamesCache"));
            Assert.False(WorldHasAmbientLease(probe.Files));
        });
    }

    private sealed class WorldReadProbe(FileSystemManager files, ValidationService validator)
    {
        internal FileSystemManager Files { get; } = files;
        internal ValidationService Validator { get; } = validator;
        internal GameEngine.PendingTurnSnapshotManifest Manifest { get; set; } = null!;
        internal FileSystemManager.BrowserOriginalOperationScope Original { get; set; } = null!;
        internal Func<string, Task>? OnRead;
        internal Func<Task>? OnOpened;
        internal int SeededOpenings;
        internal bool Armed;
        internal int PhysicalProofSpans;
        internal readonly List<string> OpenedReads = [];
        internal readonly List<(string Stage, double Seconds)> Timings = [];
        internal void Arm() { PhysicalProofSpans = 0; Timings.Clear(); OpenedReads.Clear(); Armed = true; }
    }

    private async Task WithWorldReadFixture(string layout, Func<WorldReadProbe, Task> operation,
        bool allowEstablishedReplacement = false)
    {
        WorldReadProbe? probe = null;
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                AfterCanonicalWriteLockOpenedAsync = () => probe?.Armed == true
                    ? probe.OnOpened?.Invoke() ?? Task.CompletedTask : Task.CompletedTask,
                AfterCanonicalReadInitialValidationAsync = path =>
                {
                    if (probe?.Armed != true) return Task.CompletedTask;
                    probe.OpenedReads.Add(path);
                    return probe.OnRead?.Invoke(path) ?? Task.CompletedTask;
                },
                BrowserOriginalAdmissionTimingObserver = (stage, elapsed) =>
                {
                    if (probe?.Armed != true) return;
                    probe.Timings.Add((stage, elapsed.TotalSeconds));
                    if (stage == "physical-verification") probe.PhysicalProofSpans++;
                }
            });
        if (layout == "mixed")
        {
            await files.WriteFileAtomicAsync(WorldQuestHistory, "17");
            await files.WriteFileAtomicAsync(WorldFactionChronicles, "{\"entries\":[]}");
            await files.WriteFileAtomicAsync("game_state/factions/faction_custom.json", "{\"entries\":[],\"wrong\":true}");
        }
        if (layout == "faction")
        {
            await files.WriteFileAtomicAsync(WorldFactionCore, "{\"factions\":[{\"factionId\":\"faction_original\",\"name\":\"Original\"}]}");
            await files.WriteFileAtomicAsync(WorldFactionStructure, "{\"entries\":[{\"factionId\":\"faction_original\"}],\"factionRankChanges\":[]}");
        }
        var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true, files: files);
        probe = new WorldReadProbe(files, new ValidationService(files, NullLogger<ValidationService>.Instance));
        var assertionsCompleted = false;
        try
        {
        await SessionOperationContext.RunParticipatingExpectedSessionAsync(files, staged.Binding.Generation, async () =>
        {
            using var original = files.BeginBrowserOriginalOperation(staged.Binding, staged);
            files.BeginBrowserOriginalProcessing(staged);
            probe.Original = original;
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                probe.Manifest = InvokeHeldOriginalFullProof(files, staged, lease);
            WorldEvidence(probe, new { Layout = layout, staged.Binding.ActionId, staged.Binding.Generation,
                Inputs = WorldInputImages(files), CurrentTopLevelAbsence = WorldCurrentPaths.Select(path =>
                    new { Path = path, Absent = !File.Exists(files.ResolvePath(path)) }) });
            try { await operation(probe); assertionsCompleted = true; }
            finally { probe.OnRead = null; probe.OnOpened = null; probe.Armed = false; }
            return true;
        });
        }
        catch (SessionReplacedException error) when (allowEstablishedReplacement && assertionsCompleted)
        {
            // Generation refusal may permanently mark the actual enclosing operation replaced, even after
            // test cleanup restores its bytes. Retain that expected original finalization outcome, not a retry.
            WorldEvidence(probe, new { ExpectedOuterReplacement = error.ToString(), assertionsCompleted });
        }
    }

    private static void SeedWorldOpeningCaches(WorldReadProbe probe, HashSet<string> ids, HashSet<string> names)
    {
        probe.OnOpened = () =>
        {
            if (++probe.SeededOpenings == 1)
            {
                SetPrivateField(probe.Validator, "_knownCanonicalFactionIdsCache", ids);
                SetPrivateField(probe.Validator, "_knownCanonicalFactionNamesCache", names);
            }
            return Task.CompletedTask;
        };
    }

    private void WorldEvidence(WorldReadProbe probe, object observation) => _directGachaOutput?.WriteLine(
        JsonSerializer.Serialize(new { WorldReadComponent = observation, probe.PhysicalProofSpans, probe.SeededOpenings,
            TimingSpans = probe.Timings.Select(item => new { item.Stage, item.Seconds }),
            // Physical opens only: missing reads do not fire this hook; not logical call attribution.
            PhysicalOpenedReads = probe.OpenedReads, AmbientLease = WorldHasAmbientLease(probe.Files) }));

    private static string WorldIssues(IEnumerable<ValidationIssue> issues) => JsonSerializer.Serialize(issues.Select(issue =>
        new { issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code, issue.Actor,
            issue.Section, issue.Expected, issue.Actual, issue.RepairHint, issue.RepairTargetFiles }));

    private static string WorldInputImages(FileSystemManager files) => JsonSerializer.Serialize(
        Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => { var bytes = File.ReadAllBytes(path); return new {
                Path = Path.GetRelativePath(files.GameSessionPath, path).Replace('\\', '/'), Bytes = bytes.Length,
                SHA256 = Convert.ToHexString(SHA256.HashData(bytes)), ContentBase64 = Convert.ToBase64String(bytes) }; }));

    private static bool WorldHasAmbientLease(FileSystemManager files) => (bool)typeof(FileSystemManager)
        .GetMethod("HasAmbientCanonicalLease", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(files, null)!;

    private static readonly string[] WorldCurrentPaths = [
        "game_state/world/current_location.json", "game_state/world/world_map.json", "game_state/world/world_events.json",
        "game_state/world/rival_soul_arcs.json", "game_state/world/world_flags.json", "game_state/world/world_time.json",
        "game_state/world/weather.json", "game_state/world/progression.json", "game_state/control/progression_report.json",
        "game_state/control/pending_ink_actions.json", "game_state/quests/regular_quests.json", "game_state/quests/soul_quests.json",
        WorldQuestHistory, "game_state/quests/plot_outline.json", "game_state/combat/enemies.json", "game_state/combat/allies.json",
        "game_state/combat/combat_log.json", WorldFactionCore, WorldFactionStructure, "game_state/factions/faction_resources.json",
        "game_state/factions/faction_projects.json", "game_state/factions/faction_custom.json", "game_state/factions/faction_chronicles.json"
    ];
}
