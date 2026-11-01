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
            List<ValidationIssue>? result = null;
            try
            {
                var selected = coreSelectedOut ? new GameStateValidationSelection(
                    GameStateValidationPhase.WorldQuestCombatFactionStateFiles, [WorldFactionStructure]) : WorldSelection;
                failure = await Record.ExceptionAsync(async () => { result = await probe.Validator.ValidateGameStateAsync(selected); });
                WorldEvidence(probe, new { coreSelectedOut, structureReads, ResultPublished = result != null, Failure = failure?.ToString(),
                    OriginalCore = Convert.ToHexString(SHA256.HashData(before)),
                    ChangedCore = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(corePath))) });
            }
            finally { probe.OnRead = null; File.WriteAllBytes(corePath, before); }
            Assert.Equal(coreSelectedOut ? 3 : 2, structureReads);
            Assert.Null(result);
            Assert.NotNull(failure);
            if (coreSelectedOut)
            {
                // First captured structure must be freshly compared before the nested core drift.
                Assert.Contains("RequireUnchangedReadsAsync", failure!.StackTrace);
                Assert.Equal(new[] { WorldFactionStructure, WorldFactionCore, WorldFactionCore,
                    WorldFactionStructure, WorldFactionStructure, WorldFactionCore },
                    probe.OpenedReads.Where(path => path == WorldFactionCore || path == WorldFactionStructure));
            }
            Assert.IsAssignableFrom<IOException>(failure);
            Assert.False(WorldHasAmbientLease(probe.Files));
        });
    }

    [Theory]
    [InlineData("read")]
    [InlineData("cancellation")]
    [InlineData("close")]
    [InlineData("read-and-close")]
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
            var close = new IOException("owned World lease close cut");
            var injectedClose = new WorldThrowingClose(close);
            var closeArmed = false;
            probe.OnRead = path =>
            {
                if (path != WorldQuestHistory) return Task.CompletedTask;
                if (kind is "close" or "read-and-close")
                {
                    // Actual private phase's physical lease, not a counterfeit owner or test close callback.
                    WorldPhaseLease(probe).ExternalPublicationContext = injectedClose;
                    closeArmed = true;
                }
                return kind == "close" ? Task.CompletedTask : Task.FromException(primary);
            };
            probe.Arm();
            var failure = await Record.ExceptionAsync(() => probe.Validator.ValidateGameStateAsync(WorldSelection));
            probe.OnRead = null;
            WorldEvidence(probe, new { kind, Failure = failure?.ToString(), SamePrimary = ReferenceEquals(primary, failure),
                closeArmed, injectedClose.Calls, CloseFailure = close.ToString(),
                SameSecondary = ReferenceEquals(close, failure?.Data["WorldReadPhase.CloseFailure"]) });
            if (kind == "close") Assert.Same(close, failure);
            else
            {
                Assert.Same(primary, failure);
                Assert.Contains("ReadOrdinaryFileSnapshotCoreAsync", failure!.StackTrace);
            }
            if (kind is "close" or "read-and-close") { Assert.True(closeArmed); Assert.Equal(1, injectedClose.Calls); }
            if (kind == "read-and-close") Assert.Same(close, failure!.Data["WorldReadPhase.CloseFailure"]);
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
    [InlineData("canonical-mutation")]
    [InlineData("canonical-acquire")]
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
                else if (kind == "canonical-mutation")
                {
                    var before = WorldInputImages(probe.Files);
                    rejected = await Record.ExceptionAsync(() => probe.Files.WriteFileAtomicAsync(
                        WorldPhaseLease(probe), WorldQuestHistory, "99"));
                    Assert.Equal(before, WorldInputImages(probe.Files));
                }
                else if (kind == "canonical-acquire")
                {
                    rejected = await Record.ExceptionAsync(() => probe.Files.AcquireCanonicalWriteLeaseAsync());
                    var lifecycle = await Record.ExceptionAsync(() => probe.Files.AcquireSessionLifecycleLeaseAsync());
                    WorldEvidence(probe, new { NestedLifecycleRefusal = lifecycle?.ToString() });
                    Assert.IsType<InvalidOperationException>(lifecycle);
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

    [Theory]
    [InlineData("data-io")]
    [InlineData("path-refusal")]
    public async Task WorldReadPhase_NestedCoreNullableDataAndAuthorityRemainDistinct(string kind)
    {
        await WithWorldReadFixture("faction", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var selected = new GameStateValidationSelection(
                GameStateValidationPhase.WorldQuestCombatFactionStateFiles, [WorldFactionStructure]);
            var corePath = probe.Files.ResolvePath(WorldFactionCore);
            var coreBytes = File.ReadAllBytes(corePath);
            var dataFailure = new IOException("nested current core data cut");
            Exception? firstRefusal = null;
            string? firstStack = null;
            var captureRefusal = new AsyncLocal<bool>();
            var coreReads = 0;
            var structureReads = 0;
            EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> observe = (_, args) =>
            {
                if (captureRefusal.Value && firstRefusal == null && args.Exception is InvalidDataException)
                { firstRefusal = args.Exception; firstStack = args.Exception.StackTrace; }
            };
            probe.OnRead = path =>
            {
                if (path == WorldFactionStructure) structureReads++;
                if (path != WorldFactionCore || ++coreReads != 1) return Task.CompletedTask;
                if (kind == "data-io") return Task.FromException(dataFailure);
                // Real namespace refusal in the checked reader, not an injected authority exception.
                File.Delete(corePath);
                Directory.CreateDirectory(corePath);
                captureRefusal.Value = true;
                return Task.CompletedTask;
            };
            probe.Arm();
            List<ValidationIssue>? result = null;
            Exception? failure = null;
            AppDomain.CurrentDomain.FirstChanceException += observe;
            try
            {
                failure = await Record.ExceptionAsync(async () => { result = await probe.Validator.ValidateGameStateAsync(selected); });
                WorldEvidence(probe, new { kind, coreReads, structureReads, ResultPublished = result != null,
                    Failure = failure?.ToString(), FirstRefusal = firstRefusal?.ToString(), firstStack,
                    SameDataFailure = ReferenceEquals(dataFailure, failure), SameRefusal = ReferenceEquals(firstRefusal, failure) });
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= observe;
                captureRefusal.Value = false;
                probe.OnRead = null;
                if (Directory.Exists(corePath)) Directory.Delete(corePath);
                File.WriteAllBytes(corePath, coreBytes);
            }
            Assert.Null(result);
            Assert.Equal(kind == "data-io" ? 2 : 1, coreReads);
            Assert.Equal(kind == "data-io"
                ? new[] { WorldFactionStructure, WorldFactionCore, WorldFactionCore, WorldFactionStructure }
                : new[] { WorldFactionStructure, WorldFactionCore },
                probe.OpenedReads.Where(path => path == WorldFactionCore || path == WorldFactionStructure));
            if (kind == "data-io")
            {
                // Original nullable callback finishes; its strict pair still runs in order.
                Assert.Equal(2, structureReads);
                Assert.Same(dataFailure, failure);
                Assert.Contains("ReadOrdinaryFileSnapshotCore", failure!.StackTrace);
            }
            else
            {
                Assert.Equal(1, structureReads);
                Assert.NotNull(firstRefusal);
                Assert.Same(firstRefusal, failure);
                Assert.Contains("ValidateFile", firstStack);
            }
            Assert.False(WorldHasAmbientLease(probe.Files));
        });
    }

    [Theory]
    [InlineData("retained-physical-sibling")]
    [InlineData("prior-borrow-resumes")]
    [InlineData("failed-opening")]
    [InlineData("already-running-response")]
    public async Task WorldReadPhase_ExistingAdmissionAndOpeningBranchesReleaseOnlyOwnState(string kind)
    {
        await WithWorldReadFixture("faction", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var phaseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var phaseRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task? sibling = null;
            Task<List<ValidationIssue>>? admitted = null;
            Exception? refused = null;
            Exception? siblingFailure = null;
            var openings = 0;
            var borrows = 0;
            var openingFailure = new IOException("World opening after registration cut");
            string? responseBoundary = null;
            var siblingAmbientAfter = true;
            async Task PauseSibling() { entered.TrySetResult(); await release.Task; }
            probe.Arm();
            try
            {
                if (kind == "failed-opening")
                {
                    probe.OnOpened = () => ++openings == 1 ? Task.FromException(openingFailure) : Task.CompletedTask;
                    refused = await Record.ExceptionAsync(() => probe.Validator.ValidateGameStateAsync(WorldSelection));
                    Assert.Same(openingFailure, refused);
                    Assert.Equal(1, openings);
                    Assert.Null(GetPrivateFieldValue<object?>(probe.Validator, "_worldReadPhase"));
                    Assert.Null(GetPrivateFieldValue<object?>(probe.Validator, "_worldReservation"));
                    Assert.Equal(0, GetPrivateFieldValue<int>(probe.Validator, "_worldValidationCalls"));
                    Assert.False(WorldHasAmbientLease(probe.Files));
                    probe.OnOpened = null;
                }
                else if (kind == "retained-physical-sibling")
                {
                    // Separate ExecutionContext retains a real same-access physical admission,
                    // without making the candidate's own context an already-held scalar fallback.
                    sibling = Task.Run(async () =>
                    {
                        try
                        {
                            await using (var held = await probe.Files.AcquireCanonicalWriteLeaseAsync())
                                await PauseSibling();
                        }
                        finally { siblingAmbientAfter = WorldHasAmbientLease(probe.Files); }
                    });
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    var before = WorldInputImages(probe.Files);
                    refused = await Record.ExceptionAsync(() => probe.Validator.ValidateGameStateAsync(WorldSelection));
                    Assert.IsType<InvalidOperationException>(refused);
                    Assert.False(sibling.IsCompleted);
                    Assert.Equal(before, WorldInputImages(probe.Files));
                }
                else if (kind == "prior-borrow-resumes")
                {
                    probe.OnBorrow = () => Interlocked.Increment(ref borrows) == 1 ? PauseSibling() : Task.CompletedTask;
                    sibling = Task.Run(async () =>
                    {
                        try { await using var old = await probe.Files.AcquireCanonicalWriteLeaseAsync(); }
                        finally { siblingAmbientAfter = WorldHasAmbientLease(probe.Files); }
                    });
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    probe.OnRead = async path =>
                    {
                        if (path != WorldFactionCore || phaseEntered.Task.IsCompleted) return;
                        phaseEntered.TrySetResult();
                        await phaseRelease.Task;
                    };
                    admitted = probe.Validator.ValidateGameStateAsync(WorldSelection);
                    await phaseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    release.TrySetResult();
                    siblingFailure = await Record.ExceptionAsync(() => sibling.WaitAsync(TimeSpan.FromSeconds(10)));
                    Assert.IsType<InvalidOperationException>(siblingFailure);
                    Assert.False(admitted.IsCompleted);
                    Assert.True(borrows >= 2); // Old borrow plus permitted private phase issuance.
                }
                else
                {
                    var signedCore = probe.Manifest.Files![WorldFactionCore];
                    probe.OnRead = path =>
                    {
                        if (path != signedCore || entered.Task.IsCompleted) return Task.CompletedTask;
                        responseBoundary = new System.Diagnostics.StackTrace().ToString();
                        return PauseSibling();
                    };
                    sibling = Task.Run(() =>
                    {
                        using var response = JsonDocument.Parse("{\"response\":\"owned response\",\"factions\":[]}");
                        probe.Validator.ValidateResponse(response.RootElement);
                    });
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    var ids = GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionIdsCache");
                    var names = GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionNamesCache");
                    var priorOverride = GetPrivateFieldValue<object?>(probe.Validator, "_prevalidatedPendingTurnSnapshotOverride");
                    refused = await Record.ExceptionAsync(() => probe.Validator.ValidateGameStateAsync(WorldSelection));
                    Assert.IsType<InvalidOperationException>(refused);
                    Assert.False(sibling.IsCompleted);
                    Assert.Contains("ValidateResponse", responseBoundary);
                    Assert.Same(ids, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionIdsCache"));
                    Assert.Same(names, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionNamesCache"));
                    Assert.Same(priorOverride, GetPrivateFieldValue<object?>(probe.Validator, "_prevalidatedPendingTurnSnapshotOverride"));
                }
            }
            finally
            {
                // Always settle the actual original sibling; never abandon a retained lease/task on assertion failure.
                probe.OnRead = null;
                probe.OnBorrow = null;
                probe.OnOpened = null;
                release.TrySetResult();
                phaseRelease.TrySetResult();
                if (sibling != null && siblingFailure == null)
                    siblingFailure = await Record.ExceptionAsync(() => sibling.WaitAsync(TimeSpan.FromSeconds(10)));
                if (admitted != null) await admitted.WaitAsync(TimeSpan.FromSeconds(10));
                WorldEvidence(probe, new { kind, openings, borrows, responseBoundary,
                    Refused = refused?.ToString(), SiblingFailure = siblingFailure?.ToString(), siblingAmbientAfter,
                    PhaseSettled = admitted?.IsCompletedSuccessfully,
                    ReservationReleased = GetPrivateFieldValue<object?>(probe.Validator, "_worldReservation") == null });
            }
            if (kind == "prior-borrow-resumes") Assert.IsType<InvalidOperationException>(siblingFailure);
            else Assert.Null(siblingFailure);
            if (kind is "prior-borrow-resumes" or "retained-physical-sibling") Assert.False(siblingAmbientAfter);
            Assert.Null(GetPrivateFieldValue<object?>(probe.Validator, "_worldReadPhase"));
            Assert.Null(GetPrivateFieldValue<object?>(probe.Validator, "_worldReservation"));
            Assert.Equal(0, GetPrivateFieldValue<int>(probe.Validator, "_worldValidationCalls"));
            Assert.False(WorldHasAmbientLease(probe.Files));
            // Teardown must leave both the instance and the actual access eligible for a fresh legitimate invocation.
            await probe.Validator.ValidateGameStateAsync(WorldSelection);
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

    /// <summary>
    /// Pins the reviewed World dependency sources using repository LF text across Windows and Linux checkouts.
    /// </summary>
    [Fact]
    public void WorldReadPhase_SourceClosurePinsRequireExplicitReview()
    {
        // Task-specific conservative source fence, not a general analyzer.
        // Pins include complete files of concrete World declarations/callbacks,
        // nested read adapters/caches and their admission/lease boundaries.
        // Whole-file pins deliberately require review even for unrelated edits
        // in these shared files; they do not claim an exact method count.
        (string Path, string Hash)[] pins = [
            // New direct retained-guided branch of the reviewed raw resource owner.
            ("BookOfEternityClient/Services/Validation/ValidationService.MortalGuidedTreatment.cs", "7C152DDF5AF6FE47F1339CF20684EB7C4A1F9160201311102C68B862C1C44E06"),
            ("BookOfEternityClient/Services/MortalWoundTreatmentItemPublication.Proof.cs", "29E2C704558DC8316BA3F6D20B4BF3C4ACBF23B5FD33B8543406EC69A781CB98"),
            ("BookOfEternityClient/Services/MortalWoundTreatmentResourcePublicationTransaction.CompletedItemValidation.cs", "2623DC441C1233F233FF5CC4E883EF906FC75CFA981D1AB0BE9D4FEDDD993FE5"),
            ("BookOfEternityClient/Services/Validation/ValidationService.CompletedTreatmentItemValidation.cs", "3FF3E79A861E597BE1D0E746FC72E2973D7A2179973799065A8C895FDEB54BC1"),
            ("BookOfEternityClient/Core/OperationTimingAccumulator.cs", "37DF6BC9BDC1CD793B7BFEB2CD9E32BD63544A3166D0A0D4AFFFF25FAA248B61"),
            ("BookOfEternityClient/Core/CanonicalReadActivityAccumulator.cs", "4D45F94064DFEE4DECE462A570A73B79DCDBAFE93C0B120EC7065B96060F8CC2"),
            ("BookOfEternityClient/Core/FileReadObservation.cs", "0166E281B705A3645AA357BBD7BDC8075AB7B45CE806BF2BDA7E9CF503F458C6"),
            ("BookOfEternityClient/Core/FileSystemManager.SourceReadObservation.cs", "F4F5B272495CF5CC246460A38BAA81DCDAC27EA16322C70AE9BABD0BFD088B15"),
            ("BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnReasoningSource.cs", "B1859FDB2450DD0D0EB281D2F9C7CBDF3F77DAEFBC3A57BA80FBD6F1E728EDCB"),
            ("BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnReasoningPrefix.cs", "F866EE94E11576AA5AD76408090D79B3E6840E074D64A9FEBC84DC20DF094BD6"),
            ("BookOfEternityClient/Services/Validation/ValidationService.OriginalLookupObservation.cs", "275B84781CC94E97E32C68DAB51B4150CF4F3956C64B9CE7D41CEB7E0B31FDE9"),
            ("BookOfEternityClient/Services/Validation/ValidationService.NarrativeInterfaceSources.cs", "229A2278ABDE05481F2B7FDAA8D62329AF59D247AC49BD9C4BB0ADB1DF8E2435"),
            ("BookOfEternityClient/Services/Validation/ValidationService.NarrativeInterfaceInvocation.cs", "3FDFEF8242C335ADD2979B56EA1CB6A8D1389025B8B4509194C70BA6CF4B1A4F"),
            ("BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnReasoningPolicy.cs", "C8DFD86D86A69A581C9E3FCDB960BC7B04B5D766C96B32181D465172E889BDC0"),
            ("BookOfEternityClient/Services/Validation/ValidationService.NarrativeInterfacePolicy.cs", "694D40779DCC117A798C1578E351601E8B173DE7750E0D2188EF3856C9EB2409"),
            ("BookOfEternityClient/Core/CanonicalValidationEpoch.cs", "FA22C2BDF9B3EFE06DE01630073684742EF8D46B779D11271B6CA47FFD0613A0"),
            ("BookOfEternityClient/Core/FileSystemManager.CleanupProofAdmission.cs", "1280137503498EA1F6BBC1947AA279D84C91AD02C223D8F25B130C93903D65D7"),
            ("BookOfEternityClient/Core/FileSystemManager.CleanupProofDataReads.cs", "79D6837581A8A3F07DAFE49DA6FBDEFC455CD1D3C94324921074BFD2C0E94C3B"),
            ("BookOfEternityClient/Core/FileSystemManager.ValidationReadSet.cs", "554772A72DF1E086DB458E7A666C4DE50957BEA422FBE5009BA8909B1F49B718"),
            ("BookOfEternityClient/Core/FileSystemManager.ValidationReadSet.Observations.cs", "CFF3A0CB4660BFF32D4A96616E82586E5DD343FE7A725387D7BA568EE31EC8BB"),
            ("BookOfEternityClient/Core/FileSystemManager.ValidationReadSet.CleanupData.cs", "4780FE8DE15873C048349CE64E41F324663DDB994F41A8DA68CA858082683597"),
            ("BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointRepair.cs", "58992EEE586C7E4F3A1A3B239ECEC7DEDEC987CB584D3BD21635AA6747066695"),

            ("BookOfEternityClient/Services/MortalQteDecisionPublication.cs", "18A6DABE883DFCF25C784D3B5148E923EB578E9047ADE724A8ADB7BB6161BF7F"),
            ("BookOfEternityClient/Core/FileSystemManager.ValidationReadReservation.cs", "7237190D02FE11E36A5671E82CAE499D5CD253B0D0A0FBBD69666E18C2CA8603"),
            ("BookOfEternityClient/Core/FileSystemManager.PendingSnapshotValidationRead.cs", "40B6DC5434998323A1A36E343874AD9648332EAACBC836C5DB4EF750D5EE23DE"),
            ("BookOfEternityClient/Core/FileSystemManager.ValidationMutationReservation.cs", "67F97AE0514F844DB9FBCE8FB0C91E84C2723D33E19E95F79CA6FA535DD81A8C"),
            ("BookOfEternityClient/Core/FileSystemManager.TrustedLocalStorage.cs", "5E92298EFDE6D14AF4593A77FF4AB321D03DFD08220EE2006C9A0C256D5F9EC6"),
            ("BookOfEternityClient/Core/FileSystemManager.DirectoryDeletion.cs", "5E5DBEBA9379081455F00F8F3248B8011F7147EEA77F9396189F105609770DDE"),
            ("BookOfEternityClient/Core/FileSystemManager.ImagePublication.cs", "86E30E1D4048A615002B080BA1B477D265B0831A8BED354FE7279BFD5638999F"),
            ("BookOfEternityClient/Core/FileSystemManager.WorkerPublication.cs", "2C8B54DA6A991B33D362B024B4AAAF8CC01E20C71C95DEC6ECDF4D5569F31A27"),
            ("BookOfEternityClient/Core/FileSystemManager.SyntheticBundlePublication.cs", "8A3852CF07F35AB31DEE0E28E24F3E949B3F30C70144F639846CFB2D69B92DAF"),
            ("BookOfEternityClient/Core/FileSystemManager.SessionReplacement.cs", "FAD3B8A6D5AED2D5BDD413927AD06CCAE7966E3DD193C235B8840667CFA331E9"),
            ("BookOfEternityClient/Core/FileSystemManager.DarenLocalStorage.cs", "CEB77F461A0D5EDF0EBD70AB0262CE087741199BF3AE0474B9D8E769BD7EFC31"),
            ("BookOfEternityClient/Core/FileSystemManager.BrowserLocalStorage.cs", "36742AF3AFDA505B7A002DA621875576709193F2DBCF1EB3798AE25C30BC1327"),
            ("BookOfEternityClient/Services/Validation/ValidationService.PendingSnapshotValidation.cs", "EE239CC9A9678717A69F836FEAECC1CF27E997DD51104BFB535A6C567DD48124"),
            ("BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs", "D63E1843E4DA0E1B5BB28C89E0E6004A34AFEA68806E31B4AD9ADB625D962E02"),
            ("BookOfEternityClient/Core/FileSystemManager.GameStateDataReads.cs", "2F7223C6B12D7A0DECBFBD0A50CD8A33BE83C7F39AD94BDEBBB66A78F2FDE485"),
            ("BookOfEternityClient/Core/FileSystemManager.SourceDataOperation.cs", "C1DBBDC84B2A4DA6364F9943025C2F70190B902ADB70B19EB9B29A5E89EB8A66"),
            ("BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs", "AFD319B93DA97F0EB2AD9103FE138B58647BD03F6326B9E5C2EC971ADEC72C49"),
            ("BookOfEternityClient/Core/FileSystemManager.OrdinaryReads.cs", "55E21E2782890EDE44D7CD7F7CCB4DBB34B6CCD1680F630D6B65D3EAB557770C"),
            ("BookOfEternityClient/Core/FileSystemManager.WorkerCanonicalFence.cs", "7A857EC50550365F6576B8E2CCC056E662C87622E1A32F652952760761D3E753"),
            ("BookOfEternityClient/Core/FileSystemManager.WorldValidationRead.cs", "1B1165BCDA4D378679082353A6461E7804C42B1C7446FBFE26B80EC5E2A33EAE"),
            ("BookOfEternityClient/Core/FileSystemManager.cs", "BF17FD013B7AFE38709763564481AC812CF17194A15380BBFAF7A363C2886B4F"),
            ("BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs", "8AE18B4E23A70EBD0989EA2F18D173DCB7B4503E3130AD9502F89BBD828A9DCA"),
            ("BookOfEternityClient/Services/Validation/ValidationService.ActorMaterializationBinding.cs", "22BF78161FC966A6C50CC8B7962A4105AC318C1542253BA09EF10CAAF90DC0E5"),
            ("BookOfEternityClient/Services/Validation/ValidationService.AfterlifeArchiveTradeAndLifecycle.cs", "B1802B0118538AF647648B0803B84BB92D155E5FFDD59E4C0BDD3A70FD44EDE7"),
            ("BookOfEternityClient/Services/Validation/ValidationService.AfterlifeCombatConditions.cs", "DFBD505B555F308CE7DF3B3DD6FD30FA67F662CCAE06EB59EE1A58C455953A81"),
            ("BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs", "E440D0DE36E9BBF41129A5E0AD67CB1985A40A08DD60E7B7378C748FFE092218"),
            ("BookOfEternityClient/Services/Validation/ValidationService.EffectMaterialization.cs", "7F2215EFFD2DBCCAF139A62FD90715C8433FCED75BA26CC04F44FFC534116C2F"),
            ("BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs", "C31D6A98874B052C97085DD0FEC851C7B773929DAEFB60005DCB942D57270488"),
            ("BookOfEternityClient/Services/Validation/ValidationService.FactionsAndProjects.cs", "4A59E7A4B8E12843F960F17B6A62967A414124F913EC3D3D628ECA90E91F66E6"),
            ("BookOfEternityClient/Services/Validation/ValidationService.GuardiansAndAfterlife.cs", "0CC60E658442A70540C489FAE2E0E69644CA1AAE9D8DAE987232899C95C0B6AE"),
            ("BookOfEternityClient/Services/Validation/ValidationService.InventoryNpcWorldCrossRefs.cs", "A894C83CAEABAEB74E674C66ECD1EA575ABD71A63BB7105AEF429C2E2A694FB5"),
            ("BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs", "3772467CFA87DE00E6B3BF1DC6EAE2DBE441729F449C5A9A970CB9B0C39F2D3B"),
            ("BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs", "B0E9BD001B5674935F8CB27928F8CE9C29A0E4B31271155DC6E3E3071CFAA995"),
            ("BookOfEternityClient/Services/Validation/ValidationService.MortalItemMaterialization.cs", "BC6A6F0D2B571D30108AA90CF2D3ED5A02570A8BDA35B77C2ED42E2F7C028CB5"),
            ("BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs", "4D6F4D2F868382E74B9BCCFEE2D457281DACB0AF82088AC4FDC9C84617436198"),
            ("BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs", "6EC55FE70DECF80AC11A1146548A821F0AEC445B5132D5A7C683F49A1A4B6F86"),
            ("BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs", "54222A4E3EDE3574E4A489C88F8C085A015408DDEE4C6F9531ECF87D0D5AF907"),
            ("BookOfEternityClient/Services/Validation/ValidationService.QuestsAndSoulState.cs", "D7AE9436B054878818C36759A60D1FFB44C9375EC6A58C88FAA73101BA67BB60"),
            ("BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs", "96042BF2E790C80932810689D5DC0EE2F13ED567C2BE47487EFFA05C6730C0A0"),
            ("BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs", "5820B1C3E61013705C0994F0B2581B491FB02F48577C17536866EEF964707FD6"),
            ("BookOfEternityClient/Services/Validation/ValidationService.RivalThreads.cs", "87DD3F1326CB0EBC5D4935DE145F15266FB30E0177064B8E36960C0A6A0A7576"),
            ("BookOfEternityClient/Services/Validation/ValidationService.SameTurnOwnerReadView.cs", "A8E0669C2BB1C692C2E9FA7481D78F2102267503BC31061974874DA9A4551971"),
            ("BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs", "ABA65E181F4A6F04BC92ABC14209D97989F107C732E070244350A6E4F3561E14"),
            ("BookOfEternityClient/Services/Validation/ValidationService.SpiritualWoundSourceAdmission.cs", "2DC59A55A6B9892D5EE817919247B0CDE403309283D70E709D81A2910381ED4E"),
            ("BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs", "F3E3739A31E01CB2872C480B6C9F0160C5D1E32C3A5B270FC3199D7A8EC218A6"),
            ("BookOfEternityClient/Services/Validation/ValidationService.WorldReadPhase.cs", "05586D48FAF5CF14B7FB07810E681C1B81BF4AA7675E9FDF13ACC01BE2B97D49"),
            ("BookOfEternityClient/Services/ValidationService.cs", "1DBC3F35E142D4F376B1EA4FCDF220D348B80BEE323FAE90E577D4CA5C2E9662"),
            ("BookOfEternityClient/Services/Validation/ValidationService.MetaControlShapeReadPhase.cs", "A0B1AF0D4B06A0D4C74BF31A1CFCA22FE9D902F3D7586B4D9A51888664AB7F93"),
        ];
        foreach (var (path, hash) in pins)
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(TestRepoPaths.RepoRoot, path))).Replace("\r\n", "\n")))));
        var source = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,
            "BookOfEternityClient/Services/Validation/ValidationService.WorldReadPhase.cs"));
        foreach (var path in WorldCurrentPaths) Assert.Contains('"' + path + '"', source);
        // This completed-comparison leaf is separate from legacy absent-layout fixture inputs.
        Assert.Contains("NpcCoreChangesContract.NpcCorePath", source);
        Assert.Contains("RequireFullEndProof", source);
        Assert.Contains("RequireUnchangedReadsAsync", source);
        Assert.Contains("_knownCanonicalFactionIdsCache = previousIds", source);
        Assert.Contains("_knownCanonicalFactionNamesCache = previousNames", source);
    }

    private sealed class WorldThrowingClose(Exception failure) : IDisposable
    {
        internal int Calls;
        public void Dispose()
        {
            Calls++;
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static FileSystemManager.CanonicalWriteLease WorldPhaseLease(WorldReadProbe probe)
    {
        var phase = GetPrivateFieldValue<object?>(probe.Validator, "_worldReadPhase");
        Assert.NotNull(phase);
        var lease = phase.GetType().GetProperty("Lease", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(phase);
        return Assert.IsType<FileSystemManager.CanonicalWriteLease>(lease);
    }

    private sealed class WorldReadProbe(FileSystemManager files, ValidationService validator)
    {
        internal FileSystemManager Files { get; } = files;
        internal ValidationService Validator { get; } = validator;
        internal GameEngine.PendingTurnSnapshotManifest Manifest { get; set; } = null!;
        internal FileSystemManager.BrowserOriginalOperationScope Original { get; set; } = null!;
        internal Func<string, Task>? OnRead;
        internal Func<string, Task>? OnAfterRead;
        internal Func<Task>? OnOpened;
        internal Func<Task>? OnBorrow;
        internal int SeededOpenings;
        internal bool Armed;
        internal int PhysicalProofSpans;
        internal readonly List<string> OpenedReads = [];
        internal readonly List<(string Stage, double Seconds)> Timings = [];
        internal void Arm() { PhysicalProofSpans = 0; Timings.Clear(); OpenedReads.Clear(); Armed = true; }
    }

    private async Task WithWorldReadFixture(string layout, Func<WorldReadProbe, Task> operation,
        bool allowEstablishedReplacement = false, IReadOnlyDictionary<string, string>? additionalInputs = null)
    {
        WorldReadProbe? probe = null;
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeMainBorrowRetainAsync = () => probe?.Armed == true
                    ? probe.OnBorrow?.Invoke() ?? Task.CompletedTask : Task.CompletedTask,
                AfterCanonicalWriteLockOpenedAsync = () => probe?.Armed == true
                    ? probe.OnOpened?.Invoke() ?? Task.CompletedTask : Task.CompletedTask,
                AfterCanonicalReadInitialValidationAsync = path =>
                {
                    if (probe?.Armed != true) return Task.CompletedTask;
                    probe.OpenedReads.Add(path);
                    return probe.OnRead?.Invoke(path) ?? Task.CompletedTask;
                },
                AfterCanonicalReadAttemptAsync = path => probe?.Armed == true
                    ? probe.OnAfterRead?.Invoke(path) ?? Task.CompletedTask : Task.CompletedTask,
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
        // Immutable task-local images are installed before original snapshot/authority staging.
        // Default null leaves all existing World layouts, input order and counters unchanged.
        if (additionalInputs != null)
            foreach (var (path, content) in additionalInputs)
                await files.WriteFileAtomicAsync(path, content);
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
            finally { probe.OnRead = null; probe.OnAfterRead = null; probe.OnOpened = null; probe.OnBorrow = null; probe.Armed = false; }
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
