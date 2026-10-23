using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private static readonly string[] MetaShapePaths = [
        "game_state/control/life_transitions.json",
        "game_state/control/incarnation_trigger.json",
        "game_state/control/ascension.json",
        "game_state/control/pending_shining_faction_foundings.json",
        "game_state/control/pending_shining_faction_realignments.json",
        "game_state/control/pending_shining_faction_leadership_transitions.json",
        "game_state/control/pending_npc_trade_inventory_requests.json",
        "game_state/control/pending_guardian_abode_residents_request.json",
        "game_state/control/pending_guardian_abode_resident_transfers.json",
        "game_state/control/pending_guardian_abode_resident_interactions.json",
        "game_state/control/pending_resident_companion_manifestation_request.json"
    ];
    private static readonly GameStateValidationSelection MetaShapeSelection =
        new(GameStateValidationPhase.MetaMiscStateFiles, MetaShapePaths);

    private static IReadOnlyDictionary<string, string> MetaShapeMixedInputs() => new Dictionary<string, string> {
        [MetaShapePaths[0]] = "17",
        [MetaShapePaths[1]] = "{",
        [MetaShapePaths[2]] = "[{},\"élan\"]",
        [MetaShapePaths[3]] = "{\"requests\":[{\"requestId\":\"fixed_request\",\"charter\":{\"favoredArchetype\":\"unknown\",\"patronEffectFamily\":\"bad\"}}],\"wrong\":true}",
        [MetaShapePaths[4]] = "",
        [MetaShapePaths[5]] = " \t\r\n",
        [MetaShapePaths[6]] = "{\"requests\":[],\"_fixture\":\"ёж🌙\"}",
        [MetaShapePaths[10]] = "{\"requests\":[{\"requestId\":\"fixed_manifestation\",\"manifestationSource\":\"bad\",\"relicId\":\"fixed_relic\",\"personalityProfile\":{\"coreValues\":[],\"personalityTraits\":[{\"value\":0}]},\"abodeDisposition\":{\"powerSensitivity\":\"bad\"},\"abodeDevotionLevel\":101,\"restlessness\":101,\"abodeDevotionTier\":\"bad\",\"migrationState\":\"bad\"}]}"
    };

    // The actual private synchronous issuer installs its own admission/ExecutionContext.
    // Reflection only selects the DEBUG component entry, never creates an owner or token.
    private static Task<List<ValidationIssue>> InvokeMetaShapeComponent(
        ValidationService validator, GameStateValidationSelection? selection = null)
    {
        var entry = typeof(ValidationService).GetMethod("ValidateMetaControlShapesComponentAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(entry);
        try { return Assert.IsAssignableFrom<Task<List<ValidationIssue>>>(entry.Invoke(validator, [selection ?? MetaShapeSelection])); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("mixed")]
    public async Task MetaControlShapes_WholeEntryProofCeilingPreservesOrderedIssues(string layout)
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            if (layout == "absent")
                Assert.All(MetaShapePaths, path => Assert.False(File.Exists(probe.Files.ResolvePath(path))));
            var before = WorldInputImages(probe.Files);
            var scalar = await InvokeMetaShapeComponent(probe.Validator);
            Assert.Equal(before, WorldInputImages(probe.Files));
            if (layout == "absent") Assert.Empty(scalar);
            else
            {
                Assert.Equal(new[] { "flexible_state_invalid_root", "strict_state_invalid_root" },
                    scalar.Where(issue => issue.FilePath == MetaShapePaths[0]).Select(issue => issue.Code));
                Assert.Equal(new[] { "flexible_state_invalid_json", "strict_state_invalid_json" },
                    scalar.Where(issue => issue.FilePath == MetaShapePaths[1]).Select(issue => issue.Code));
                Assert.Equal(new[] { "strict_state_invalid_root" },
                    scalar.Where(issue => issue.FilePath == MetaShapePaths[2]).Select(issue => issue.Code));
                Assert.DoesNotContain(scalar, issue => issue.FilePath == MetaShapePaths[4] || issue.FilePath == MetaShapePaths[5]);
                Assert.Equal(System.Text.Encoding.UTF8.GetPreamble().Concat(
                    System.Text.Encoding.UTF8.GetBytes(MetaShapeMixedInputs()[MetaShapePaths[6]])).ToArray(),
                    File.ReadAllBytes(probe.Files.ResolvePath(MetaShapePaths[6])));
                Assert.Contains(scalar, issue => issue.Code == "shining_abode_invalid_favored_archetype");
                Assert.Contains(scalar, issue => issue.Code == "guardian_abode_resident_personality_trait_value_out_of_bounds");
            }
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            probe.Arm(); // Before real synchronous admission, through returned issue list and finally.
            List<ValidationIssue>? actual = null;
            Exception? failure = null;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { actual = await InvokeMetaShapeComponent(probe.Validator); }
            catch (Exception error) { failure = error; throw; }
            finally { WorldEvidence(probe, new { MetaControlShapes = layout, before, SourceDeclaredReadCalls = 22,
                InvocationSeconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds,
                ScalarIssues = WorldIssues(scalar), ActualIssues = actual == null ? null : WorldIssues(actual),
                Failure = failure?.ToString(), GuardContextPresent = MetaShapeContextRefs(probe.Validator).Select(value => value != null).ToArray() }); }
            Assert.Equal(before, WorldInputImages(probe.Files));
            Assert.Equal(WorldIssues(scalar), WorldIssues(actual!));
            Assert.False(WorldHasAmbientLease(probe.Files));
            Assert.InRange(probe.PhysicalProofSpans, 1, 6);
        }, additionalInputs: layout == "mixed" ? MetaShapeMixedInputs() : null);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("cancel")]
    [InlineData("close")]
    [InlineData("read-and-close")]
    public async Task MetaControlShapes_AsyncFailureIsImmediateOriginalAndDoesNotPublish(string kind)
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            Exception primary = kind == "cancel" ? new OperationCanceledException("owned Meta cancellation cut", new CancellationToken(true)) :
                new IOException("owned Meta async read cut");
            var close = new IOException("owned Meta actual owning close cut");
            var closeProbe = new MetaOwningCloseProbe(probe, close);
            var ids = new HashSet<string> { "prior_id" };
            var names = new HashSet<string> { "Prior name" };
            SeedMetaShapeOpeningCaches(probe, ids, names);
            var reached = 0;
            probe.OnRead = path =>
            {
                if (path != MetaShapePaths[0] || ++reached != 1) return Task.CompletedTask;
                if (kind is "close" or "read-and-close") MetaShapePhaseLease(probe).ExternalPublicationContext = closeProbe;
                return kind == "close" ? Task.CompletedTask : Task.FromException(primary);
            };
            probe.Arm();
            List<ValidationIssue>? result = null;
            var failure = await Record.ExceptionAsync(async () => { result = await InvokeMetaShapeComponent(probe.Validator); });
            WorldEvidence(probe, new { kind, reached, Failure = failure?.ToString(), SamePrimary = ReferenceEquals(primary, failure),
                ResultPublished = result != null, closeProbe.Calls, closeProbe.ProofsAtClose,
                SameSecondary = ReferenceEquals(close, failure?.Data["MetaControlShapes.CloseFailure"]) });
            if (kind == "close")
            {
                Assert.Same(close, failure);
                Assert.True(closeProbe.ProofsAtClose >= 5); // Successful end/readset, then actual owning Dispose.
            }
            else
            {
                Assert.Equal(1, reached);
                Assert.Same(primary, failure);
                Assert.Contains("ReadOrdinaryFileSnapshotCoreAsync", failure!.StackTrace);
            }
            if (kind is "close" or "read-and-close") Assert.Equal(1, closeProbe.Calls);
            if (kind == "read-and-close") Assert.Same(close, failure!.Data["MetaControlShapes.CloseFailure"]);
            Assert.Null(result);
            Assert.True(probe.SeededOpenings > 0);
            Assert.Same(ids, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionIdsCache"));
            Assert.Same(names, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionNamesCache"));
            Assert.Equal(new[] { "prior_id" }, ids);
            Assert.Equal(new[] { "Prior name" }, names);
            Assert.False(WorldHasAmbientLease(probe.Files));
        }, additionalInputs: MetaShapeMixedInputs());
    }

    [Theory]
    [InlineData("repeat")]
    [InlineData("end-readset")]
    public async Task MetaControlShapes_RepeatedCurrentDriftNeverPublishesIssues(string boundary)
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var path = probe.Files.ResolvePath(MetaShapePaths[0]);
            var before = File.ReadAllBytes(path);
            var reads = 0;
            probe.OnAfterRead = relative =>
            {
                // Existing after-attempt hook runs after the first data capture; mutate before strict read.
                if (relative == MetaShapePaths[0] && ++reads == (boundary == "repeat" ? 1 : 2))
                    File.WriteAllText(path, "{\"reason\":\"Voluntary\",\"summary\":\"changed\"}");
                return Task.CompletedTask;
            };
            probe.Arm();
            List<ValidationIssue>? result = null;
            Exception? failure;
            try { failure = await Record.ExceptionAsync(async () => { result = await InvokeMetaShapeComponent(probe.Validator); }); }
            finally { probe.OnAfterRead = null; File.WriteAllBytes(path, before); }
            WorldEvidence(probe, new { boundary, reads, Failure = failure?.ToString(), ResultPublished = result != null });
            Assert.Null(result);
            Assert.IsAssignableFrom<IOException>(failure);
            Assert.Contains(boundary == "repeat" ? "ReadTextAsync" : "RequireUnchangedReadsAsync", failure!.StackTrace);
            Assert.False(WorldHasAmbientLease(probe.Files));
        }, additionalInputs: MetaShapeMixedInputs());
    }

    [Fact]
    public async Task MetaControlShapes_PhaseLeavesNonNullFactionCachesAndGuardianStateUntouched()
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var ids = new HashSet<string> { "prior_id" };
            var names = new HashSet<string> { "Prior name" };
            SeedMetaShapeOpeningCaches(probe, ids, names);
            var outer = MetaShapeContextRefs(probe.Validator);
            probe.Arm();
            var issues = await InvokeMetaShapeComponent(probe.Validator);
            WorldEvidence(probe, new { OrderedIssues = WorldIssues(issues), GuardContextPresent = MetaShapeContextRefs(probe.Validator).Select(value => value != null).ToArray() });
            Assert.True(probe.SeededOpenings > 0);
            Assert.Same(ids, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionIdsCache"));
            Assert.Same(names, GetPrivateFieldValue<object?>(probe.Validator, "_knownCanonicalFactionNamesCache"));
            Assert.Equal(new[] { "prior_id" }, ids);
            Assert.Equal(new[] { "Prior name" }, names);
            var after = MetaShapeContextRefs(probe.Validator);
            for (var i = 0; i < outer.Length; i++) Assert.Same(outer[i], after[i]);
            Assert.False(WorldHasAmbientLease(probe.Files));
        }, additionalInputs: MetaShapeMixedInputs());
    }

    [Theory]
    [InlineData("wrong-phase")]
    [InlineData("unknown-path")]
    [InlineData("unrestricted")]
    public async Task MetaControlShapes_ComponentRejectsWidenedDispatchBeforeAdmission(string kind)
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var selection = kind == "wrong-phase" ? new GameStateValidationSelection(GameStateValidationPhase.RequiredFiles, MetaShapePaths) :
                kind == "unknown-path" ? new GameStateValidationSelection(GameStateValidationPhase.MetaMiscStateFiles,
                    MetaShapePaths.Concat(["game_state/control/pending_turn_snapshot.json"])) :
                new GameStateValidationSelection(GameStateValidationPhase.MetaMiscStateFiles);
            probe.Arm();
            var failure = await Record.ExceptionAsync(() => InvokeMetaShapeComponent(probe.Validator, selection));
            WorldEvidence(probe, new { kind, Failure = failure?.ToString() });
            Assert.IsType<ArgumentException>(failure);
            Assert.Equal(0, probe.PhysicalProofSpans);
            Assert.False(WorldHasAmbientLease(probe.Files));
            // Rejected fixed dispatch must not install a real admission or leak shared call count.
            Assert.Empty(await InvokeMetaShapeComponent(probe.Validator));
        });
    }

    [Theory]
    [InlineData("already-running-scalar")]
    [InlineData("new-game-state")]
    [InlineData("new-response")]
    [InlineData("override-install")]
    [InlineData("override-dispose")]
    [InlineData("canonical-mutation")]
    [InlineData("canonical-acquire")]
    public async Task MetaControlShapes_SiblingsAndOverrideCannotMutateReservedInvocation(string kind)
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = kind == "already-running-scalar" ? null :
                probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var pauses = 0;
            probe.OnRead = path => path == MetaShapePaths[0] && Interlocked.Increment(ref pauses) == 1
                ? Pause() : Task.CompletedTask;
            async Task Pause() { entered.TrySetResult(); await release.Task; }
            probe.Arm();
            var admitted = InvokeMetaShapeComponent(probe.Validator);
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
                    rejected = await Record.ExceptionAsync(() => InvokeMetaShapeComponent(probe.Validator));
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
                        MetaShapePhaseLease(probe), MetaShapePaths[0], "99"));
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
        }, additionalInputs: MetaShapeMixedInputs());
    }

    [Theory]
    [InlineData("retained-physical-sibling")]
    [InlineData("prior-borrow-resumes")]
    [InlineData("failed-opening")]
    [InlineData("already-running-response")]
    public async Task MetaControlShapes_OriginalAdmissionOpeningAndResponseBranchesReleaseOnlyOwnState(string kind)
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
                    refused = await Record.ExceptionAsync(() => InvokeMetaShapeComponent(probe.Validator));
                    Assert.Same(openingFailure, refused);
                    Assert.Equal(1, openings);
                    Assert.Null(GetPrivateFieldValue<object?>(probe.Validator, "_metaControlShapeReadPhase"));
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
                    refused = await Record.ExceptionAsync(() => InvokeMetaShapeComponent(probe.Validator));
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
                        if (path != MetaShapePaths[0] || phaseEntered.Task.IsCompleted) return;
                        phaseEntered.TrySetResult();
                        await phaseRelease.Task;
                    };
                    admitted = InvokeMetaShapeComponent(probe.Validator);
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
                    refused = await Record.ExceptionAsync(() => InvokeMetaShapeComponent(probe.Validator));
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
            Assert.Null(GetPrivateFieldValue<object?>(probe.Validator, "_metaControlShapeReadPhase"));
            Assert.Null(GetPrivateFieldValue<object?>(probe.Validator, "_worldReservation"));
            Assert.Equal(0, GetPrivateFieldValue<int>(probe.Validator, "_worldValidationCalls"));
            Assert.False(WorldHasAmbientLease(probe.Files));
            // Teardown must leave both the instance and the actual access eligible for a fresh legitimate invocation.
            await InvokeMetaShapeComponent(probe.Validator);
            Assert.False(WorldHasAmbientLease(probe.Files));
        }, additionalInputs: MetaShapeMixedInputs());
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("owner-before-read")]
    [InlineData("owner-on-close")]
    [InlineData("override-withdraw")]
    public async Task MetaControlShapes_ContinuityLossNeverPublishesIncludingActualOwningClose(string kind)
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var generationBefore = File.ReadAllBytes(probe.Files.SessionGenerationPath);
            var close = new MetaOwningCloseProbe(probe, beforeClose: () => probe.Original.Dispose());
            var reached = 0;
            probe.OnRead = path =>
            {
                if (path != MetaShapePaths[0] || ++reached != 1) return Task.CompletedTask;
                if (kind == "owner-on-close") MetaShapePhaseLease(probe).ExternalPublicationContext = close;
                else if (kind == "owner-before-read") probe.Original.Dispose();
                else if (kind == "override-withdraw") snapshot.Dispose();
                else
                {
                    var changed = JsonNode.Parse(generationBefore)!;
                    changed["GenerationId"] = Guid.NewGuid().ToString("N");
                    File.WriteAllText(probe.Files.SessionGenerationPath, changed.ToJsonString());
                }
                return Task.CompletedTask;
            };
            probe.Arm();
            List<ValidationIssue>? result = null;
            Exception? failure;
            try { failure = await Record.ExceptionAsync(async () => { result = await InvokeMetaShapeComponent(probe.Validator); }); }
            finally { probe.OnRead = null; File.WriteAllBytes(probe.Files.SessionGenerationPath, generationBefore); }
            WorldEvidence(probe, new { kind, reached, close.Calls, close.ProofsAtClose,
                Failure = failure?.ToString(), ResultPublished = result != null });
            Assert.True(reached > 0);
            Assert.NotNull(failure);
            Assert.Null(result);
            if (kind == "owner-on-close")
            {
                Assert.Equal(1, close.Calls);
                Assert.True(close.ProofsAtClose >= 5);
                Assert.Contains("RequireOwnerAfterClose", failure!.StackTrace);
            }
            Assert.False(WorldHasAmbientLease(probe.Files));
        }, allowEstablishedReplacement: kind == "generation", additionalInputs: MetaShapeMixedInputs());
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("zero-window")]
    [InlineData("held")]
    [InlineData("draft")]
    public async Task MetaControlShapes_EligibilityKeepsActualScalarDraftAndZeroWindowBoundaries(string kind)
    {
        const string social = "game_state/control/pending_guardian_social_interactions.json";
        var inputs = new Dictionary<string, string>(MetaShapeMixedInputs()) { [social] = "{\"requests\":[]}" };
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var metaOpenings = 0;
            probe.OnOpened = () =>
            {
                if (GetPrivateFieldValue<object?>(probe.Validator, "_metaControlShapeReadPhase") != null) metaOpenings++;
                return Task.CompletedTask;
            };
            probe.Arm();
            List<ValidationIssue> issues;
            if (kind == "partial")
            {
                issues = await InvokeMetaShapeComponent(probe.Validator,
                    new(GameStateValidationPhase.MetaMiscStateFiles, [MetaShapePaths[0]]));
                Assert.Equal(new[] { "flexible_state_invalid_root", "strict_state_invalid_root" }, issues.Select(issue => issue.Code));
                Assert.All(probe.OpenedReads, path => Assert.Equal(MetaShapePaths[0], path));
                Assert.Equal(1, metaOpenings);
                Assert.InRange(probe.PhysicalProofSpans, 1, 6);
            }
            else if (kind == "zero-window")
            {
                // Real ordinary insertion still runs neighbors; only selected shape window opens are counted here.
                issues = await probe.Validator.ValidateGameStateAsync(
                    new GameStateValidationSelection(GameStateValidationPhase.MetaMiscStateFiles, [social]));
                Assert.Equal(0, metaOpenings);
                // The unchanged scalar lifecycle dispatcher may read these paths outside this shape window.
            }
            else if (kind == "held")
            {
                await using (var existing = await probe.Files.AcquireCanonicalWriteLeaseAsync())
                {
                    issues = await InvokeMetaShapeComponent(probe.Validator);
                    Assert.True(existing.IsActive);
                    Assert.True(WorldHasAmbientLease(probe.Files));
                    Assert.Equal(0, metaOpenings); // Existing legitimate held scalar path; no nested phase lease.
                }
            }
            else
            {
                var images = MetaShapePaths.ToDictionary(path => path,
                    path => new CanonicalBeforeImage(true, System.Text.Encoding.UTF8.GetBytes("{}")));
                images[MetaShapePaths[0]] = new CanonicalBeforeImage(false, null);
                Assert.True(File.Exists(probe.Files.ResolvePath(MetaShapePaths[0])));
                var draft = SpiritualOriginalDraftInputs.Create("fixed_session", "fixed_request", "fixed_snapshot", 1, MetaShapePaths, images);
                // Construct the actual immutable draft reader through its original constructor; never forge authority.
                var ctor = typeof(ValidationService).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                    null, [typeof(FileSystemManager), typeof(Microsoft.Extensions.Logging.ILogger<ValidationService>), typeof(SpiritualOriginalDraftInputs)], null)!;
                var validator = Assert.IsType<ValidationService>(ctor.Invoke([probe.Files, NullLogger<ValidationService>.Instance, draft]));
                var scalar = await InvokeMetaShapeComponent(validator);
                using var bound = validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
                issues = await InvokeMetaShapeComponent(validator);
                Assert.Equal(WorldIssues(scalar), WorldIssues(issues));
                Assert.DoesNotContain(issues, issue => issue.FilePath == MetaShapePaths[0]);
                Assert.Null(draft.ReadText(MetaShapePaths[0])); // Registered absence wins over present physical bytes.
                var subset = MetaShapePaths.Skip(1).ToArray();
                var limitedDraft = SpiritualOriginalDraftInputs.Create("fixed_session", "fixed_request", "fixed_snapshot", 1,
                    subset, subset.ToDictionary(path => path, path => images[path]));
                var limited = Assert.IsType<ValidationService>(ctor.Invoke([probe.Files, NullLogger<ValidationService>.Instance, limitedDraft]));
                using var limitedBound = limited.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
                var refused = await Record.ExceptionAsync(() => InvokeMetaShapeComponent(limited,
                    new(GameStateValidationPhase.MetaMiscStateFiles, [MetaShapePaths[0]])));
                Assert.IsType<KeyNotFoundException>(refused);
                Assert.Contains("ReadImage", refused!.StackTrace);
                Assert.Empty(probe.OpenedReads); // Registered draft priority/refusal; no physical fallback.
                Assert.Equal(0, metaOpenings);
            }
            WorldEvidence(probe, new { kind, metaOpenings, Issues = WorldIssues(issues),
                MetaPhaseReleased = GetPrivateFieldValue<object?>(probe.Validator, "_metaControlShapeReadPhase") == null,
                ReservationReleased = GetPrivateFieldValue<object?>(probe.Validator, "_worldReservation") == null });
            Assert.Null(GetPrivateFieldValue<object?>(probe.Validator, "_metaControlShapeReadPhase"));
            Assert.Null(GetPrivateFieldValue<object?>(probe.Validator, "_worldReservation"));
            Assert.Equal(0, GetPrivateFieldValue<int>(probe.Validator, "_worldValidationCalls"));
            Assert.False(WorldHasAmbientLease(probe.Files));
        }, additionalInputs: inputs);
    }

    [Fact]
    public async Task MetaControlShapes_ActualPathRefusalEscapesImmediatelySameException()
    {
        await WithWorldReadFixture("meta", async probe =>
        {
            using var snapshot = probe.Validator.UsePrevalidatedPendingTurnSnapshotScope(probe.Manifest);
            var path = probe.Files.ResolvePath(MetaShapePaths[0]);
            var before = File.ReadAllBytes(path);
            var observing = new AsyncLocal<bool>();
            Exception? first = null;
            string? stack = null;
            EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
            {
                if (observing.Value && first == null && args.Exception is InvalidDataException)
                { first = args.Exception; stack = args.Exception.StackTrace; }
            };
            var reached = 0;
            probe.OnRead = relative =>
            {
                if (relative != MetaShapePaths[0] || ++reached != 1) return Task.CompletedTask;
                File.Delete(path);
                Directory.CreateDirectory(path); // Actual trusted leaf-type refusal after original open.
                observing.Value = true;
                return Task.CompletedTask;
            };
            probe.Arm();
            List<ValidationIssue>? result = null;
            Exception? failure;
            AppDomain.CurrentDomain.FirstChanceException += observe;
            try { failure = await Record.ExceptionAsync(async () => { result = await InvokeMetaShapeComponent(probe.Validator); }); }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= observe;
                observing.Value = false;
                probe.OnRead = null;
                if (Directory.Exists(path)) Directory.Delete(path);
                File.WriteAllBytes(path, before);
            }
            WorldEvidence(probe, new { reached, Failure = failure?.ToString(), FirstRefusal = first?.ToString(), stack,
                SameRefusal = ReferenceEquals(first, failure), ResultPublished = result != null });
            Assert.Equal(1, reached);
            Assert.NotNull(first);
            Assert.Same(first, failure);
            Assert.Contains("ValidateFile", stack);
            Assert.Null(result);
            Assert.Equal(new[] { MetaShapePaths[0] }, probe.OpenedReads);
            Assert.False(WorldHasAmbientLease(probe.Files));
        }, additionalInputs: MetaShapeMixedInputs());
    }

    /// <summary>
    /// Pins the reviewed Meta dependency sources using repository LF text across Windows and Linux checkouts.
    /// </summary>
    [Fact]
    public void MetaControlShapes_SourceClosurePinsRequireReview()
    {
        // Conservative concrete declarations, callbacks, primitive/type initialization,
        // issue const-path classification and real authority boundaries. Not a method roster/analyzer.
        (string Path, string Hash)[] pins = [
            ("BookOfEternityClient/Core/CanonicalValidationEpoch.cs", "FA22C2BDF9B3EFE06DE01630073684742EF8D46B779D11271B6CA47FFD0613A0"),
            ("BookOfEternityClient/Core/FileSystemManager.CleanupProofAdmission.cs", "1280137503498EA1F6BBC1947AA279D84C91AD02C223D8F25B130C93903D65D7"),
            ("BookOfEternityClient/Core/FileSystemManager.CleanupProofDataReads.cs", "79D6837581A8A3F07DAFE49DA6FBDEFC455CD1D3C94324921074BFD2C0E94C3B"),
            ("BookOfEternityClient/Core/FileSystemManager.ValidationReadSet.cs", "D476CFEA45AEF725E5F4FBE65EFBD589C23B31727217FFE70B6BDBA7969E9869"),
            ("BookOfEternityClient/Core/FileSystemManager.ValidationReadSet.Observations.cs", "CFF3A0CB4660BFF32D4A96616E82586E5DD343FE7A725387D7BA568EE31EC8BB"),
            ("BookOfEternityClient/Core/FileSystemManager.ValidationReadSet.CleanupData.cs", "4780FE8DE15873C048349CE64E41F324663DDB994F41A8DA68CA858082683597"),
            ("BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointRepair.cs", "028FF17649A12C3313433977789BE7D5964124F8737EB9E1CDC0B1AF06C947C4"),

            ("BookOfEternityClient/Services/MortalQteDecisionPublication.cs", "18A6DABE883DFCF25C784D3B5148E923EB578E9047ADE724A8ADB7BB6161BF7F"),
            ("BookOfEternityClient/Core/FileSystemManager.ValidationReadReservation.cs", "7237190D02FE11E36A5671E82CAE499D5CD253B0D0A0FBBD69666E18C2CA8603"),
            ("BookOfEternityClient/Core/FileSystemManager.PendingSnapshotValidationRead.cs", "21F83508A8FEB6748408FAA34A827745E8FBCDC22F48DC2168910ED134B01FF0"),
            ("BookOfEternityClient/Core/FileSystemManager.ValidationMutationReservation.cs", "2FF09FF9D1F05135347A13174DD053888D01E5ABDE77DC5E5F11D2946D8DDCA9"),
            ("BookOfEternityClient/Core/FileSystemManager.DirectoryDeletion.cs", "5E5DBEBA9379081455F00F8F3248B8011F7147EEA77F9396189F105609770DDE"),
            ("BookOfEternityClient/Core/FileSystemManager.ImagePublication.cs", "86E30E1D4048A615002B080BA1B477D265B0831A8BED354FE7279BFD5638999F"),
            ("BookOfEternityClient/Core/FileSystemManager.WorkerPublication.cs", "2C8B54DA6A991B33D362B024B4AAAF8CC01E20C71C95DEC6ECDF4D5569F31A27"),
            ("BookOfEternityClient/Core/FileSystemManager.SyntheticBundlePublication.cs", "8A3852CF07F35AB31DEE0E28E24F3E949B3F30C70144F639846CFB2D69B92DAF"),
            ("BookOfEternityClient/Core/FileSystemManager.SessionReplacement.cs", "FAD3B8A6D5AED2D5BDD413927AD06CCAE7966E3DD193C235B8840667CFA331E9"),
            ("BookOfEternityClient/Core/FileSystemManager.DarenLocalStorage.cs", "CEB77F461A0D5EDF0EBD70AB0262CE087741199BF3AE0474B9D8E769BD7EFC31"),
            ("BookOfEternityClient/Core/FileSystemManager.BrowserLocalStorage.cs", "36742AF3AFDA505B7A002DA621875576709193F2DBCF1EB3798AE25C30BC1327"),
            ("BookOfEternityClient/Services/Validation/ValidationService.PendingSnapshotValidation.cs", "5D4B48AFC21F93044ACA61B1FB7C2B53EA3D1CD45D199C7E19F5FA7D59B5D51B"),
            ("BookOfEternityClient/Configuration/SharedJsonOptions.cs", "AF3A89A08074879DE8F9CCA1E1F94BCCADFB944D4D75B8FAA861ED5A0D2562E2"),
            ("BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs", "1DECB1BEB44D2FD0466199E58FF06FB6B39E7795341493FE8DA09A977EC8D202"),
            ("BookOfEternityClient/Core/FileSystemManager.GameStateDataReads.cs", "7600ED52F09691741DF9D0B269BA08BD0461FD6F33B1CB729212FA6534ACCBE5"),
            ("BookOfEternityClient/Core/FileSystemManager.SourceDataOperation.cs", "203E7C5541303B030E768369530DA4A32AD919CEC0EA8B081F9F7C7DA3349539"),
            ("BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs", "E22CD17C91DE7B5DFB06A88E9032E8A53DE6D1E7338324C5740D0EFBD9CBFDBF"),
            ("BookOfEternityClient/Core/FileSystemManager.OrdinaryReads.cs", "FD09C679CC9627F5ABB1B60150733E8E90912E305504495F932D9C3F59CBF720"),
            ("BookOfEternityClient/Core/FileSystemManager.WorkerCanonicalFence.cs", "7A857EC50550365F6576B8E2CCC056E662C87622E1A32F652952760761D3E753"),
            ("BookOfEternityClient/Core/FileSystemManager.WorldValidationRead.cs", "B300E4FA9496502F1D2AB3DA25505D588463CD596E82B1B78FAE0BCDDF53A0F4"),
            ("BookOfEternityClient/Core/FileSystemManager.cs", "B590AAF996762EB5760EB53C50B99CA1820E000FBEE989AA936F47FA1312D025"),
            ("BookOfEternityClient/Core/IncarnationTriggerContract.cs", "D406204075B1C12EA02DF1E0AAEE371CABD7E0887D2A21FC97B156FF6F0E3894"),
            ("BookOfEternityClient/Services/ActorSocialInteractionRequestState.cs", "03E254BE75F21A61C6D045A2121B83DCA89C7F9D9FEB25F98E00361E514867B7"),
            ("BookOfEternityClient/Services/AfterlifeActiveThreatState.cs", "2D3F5A41E9C16E9F00A7380FF91156778907548E4FD4BBCD836CD0437C58E048"),
            ("BookOfEternityClient/Services/AfterlifeArchiveActionState.cs", "5EB55664C0E5BAF335E79DBE534F6438DBAC68AB39DAC512393BCA8691A39246"),
            ("BookOfEternityClient/Services/AfterlifeArchiveCandidateService.cs", "AB38D24FA9EE1BD4CD683BAD5CCF425B5768400477B972942D8EB1B2F26B7B24"),
            ("BookOfEternityClient/Services/AfterlifeChronicleState.cs", "89349D326AA8387D5CD60E41B0BDC9AE115016B6D251D2AEE0CFBC0B830B1E90"),
            ("BookOfEternityClient/Services/AfterlifeContractRegistry.cs", "BAF73C92D63578063E92904277503DCC3322CD57C7DB38F47734FA5C4A181C7A"),
            ("BookOfEternityClient/Services/AfterlifeEntityProfileState.cs", "13B716101E4F111435E5A7BF5FE5FA0083B4904B60A0CEC920D9512B6EFCC885"),
            ("BookOfEternityClient/Services/AfterlifeGlobalFlagState.cs", "A691456B31CEF9901855C1351BB936084BDA65724CD0A055C6353FF8C9ECF171"),
            ("BookOfEternityClient/Services/AfterlifeNotificationState.cs", "E2A7998461720231E3A8F708AE5BD899419034485FC49B7EEED6E703E80991C2"),
            ("BookOfEternityClient/Services/AfterlifeReturnGuardService.cs", "1E10DDC29700AA3C312DE010A89A3A033891518F3200BBB5C589B313DC601EE3"),
            ("BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs", "5D6B770BDF7A4BDE1429ADDBB89CD8354FEE99303716DC4EF8106E128EEC5EBC"),
            ("BookOfEternityClient/Services/AfterlifeStoryOutlineState.cs", "397E7AFB201297296F2289A11222F19B0679A82CCBD36C5BB5110325091C8911"),
            ("BookOfEternityClient/Services/ChaosSeaGuardianPoliticsState.cs", "97EA600156C907B064C57E1154DCB2A486207F59C859015083DD069EBF8445B0"),
            ("BookOfEternityClient/Services/CraftRequestState.cs", "4C9324BF07052F145E34483F958306D1A04D111C56DDFC515526048D91EA15DB"),
            ("BookOfEternityClient/Services/GuardianAbodeOfferingState.cs", "94CAA7D66133344BB088050CE87F5CD1815E72B19109BF21446B918392803DB4"),
            ("BookOfEternityClient/Services/GuardianAbodeResidentRequestState.cs", "4C7BCDB716281E961B5882B10D1D378C5804BC52C2A7285F56EC1E329E80EBE7"),
            ("BookOfEternityClient/Services/GuardianAbodeResidentState.cs", "84ED91EFEA8EB31538CAC667100A99F70AAB2EBA2F86D5F3153022796544BC42"),
            ("BookOfEternityClient/Services/GuardianCorrectionService.cs", "4BB3F34DE7DC2341CF7C7481A5787158B557085F091571F726BE8D0963F113B5"),
            ("BookOfEternityClient/Services/GuardianTradeRequestState.cs", "5B69B4A71660BD9BCC899B6B184AACCBFA877E0C325DB5E9A58B738A22822B3D"),
            ("BookOfEternityClient/Services/MortalItemIdentityState.cs", "50034BA7D600EA21E8DA0C34C878719B6CDFC2BB28FB532FE869E3094FD801D2"),
            ("BookOfEternityClient/Services/MortalItemRepairPacketBuilder.cs", "7FD2A3E3AE98F9D5BEBAE6A31CBFB95625A26733E105B870A7789A6F46E9F741"),
            ("BookOfEternityClient/Services/MortalLocationIdentityState.cs", "CCFB17A09FD2591179E502FE8B0CDA3FC10F74B6E50D7A593FEAE78ABE35C19C"),
            ("BookOfEternityClient/Services/MortalLocationStorageContentsState.cs", "27D01910E64B2E8E13CFED19E2FCE7FFBADABA7BBC78BFED71F415A07EFF2A25"),
            ("BookOfEternityClient/Services/NpcTradeRequestState.cs", "24304EB1D54546701601E77C94267A639F324073553C907F15701682D9738C49"),
            ("BookOfEternityClient/Services/NpcTradeService.Data.cs", "F1E9C6141476CC4FF815BADCCB7F6CCD29F84F9E7CAC0AA6B96C631119D3FBE4"),
            ("BookOfEternityClient/Services/NpcTradeService.cs", "C7F569A8E7E5C2B0732D935E2642FA5B6BEB8F3EFA252C3B3EBC67469025BA57"),
            ("BookOfEternityClient/Services/PendingTurnSnapshotAuthority.cs", "60F4D5A99214EBA658955621C8BE758AB20D60013542155FF219C093266470CA"),
            ("BookOfEternityClient/Services/PlayerGuardianFoundationState.cs", "0BBF9E8D3D810CEBF3E302A0D9CDC30E836C697B2B32E5F16F07A382A728BB8D"),
            ("BookOfEternityClient/Services/ProgressionScheduleService.cs", "D0CB8CBF0B6B67F7CA8F351847817FA2D5AD6E98712C8A39ECBBD18EBB7B88FF"),
            ("BookOfEternityClient/Services/QteSceneService.cs", "D65794D5FB82ED164AF35858298E78B1B34C20F3B30A71BEEF427182EEBC7F1F"),
            ("BookOfEternityClient/Services/RealmSegregationAutoRollbackService.cs", "DA6FBD7AD4792D9FD7A53F3BC4D9ADCBD7E3C943850773CE6F91182997629289"),
            ("BookOfEternityClient/Services/SarefMainStoryState.cs", "E0FCEB10F412B90FD07A696D55362C66DA4272A730B6095A9F9E09E516861D2C"),
            ("BookOfEternityClient/Services/ScenarioCoreService.cs", "9C5A20E4DB01DC66C6F38908AF0F541611D6AFC0E7FA6D239424B48D4F5F4088"),
            ("BookOfEternityClient/Services/ShiningAbodeState.FactionPoliticalMemory.cs", "0FB30EFDFD32BF4D68A91984A8DCDD07559D99D9E685A7C3E1C70D8DF139FCC4"),
            ("BookOfEternityClient/Services/ShiningAbodeState.Gacha.cs", "DC58B9E08F6BA3E49EEDD26440ED863675DA95D0AF56A1B964F3E7026F56364B"),
            ("BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs", "40B4BBBED214A25FAC9DB938D43DA32ED1B6EB4322C1A8C8A95696CDD5E9DD47"),
            ("BookOfEternityClient/Services/ShiningAbodeState.TradeAndForge.cs", "9415D4EEB4F77829A5BDF3F944479D15F89D3AD76BDACA8452639CC1054A2B52"),
            ("BookOfEternityClient/Services/ShiningAbodeState.Treasury.cs", "EAEB1A199079A697DEE1AAD48962AF509D3B482C72D19129DA9EB5A4CFCA1817"),
            ("BookOfEternityClient/Services/ShiningAbodeState.cs", "940ED36904BE677527CFB8D5FD714BA53F5A20AB7700B54407903907E6D19873"),
            ("BookOfEternityClient/Services/ShiningCoreActionRequestState.cs", "39F804C0CCD0681D18E6D4022702EFBCE1C307F0F107401A264E86685B9A6374"),
            ("BookOfEternityClient/Services/ShiningFactionRequestState.cs", "5D6F11EAC11A25FEC640F5377FA31701F72B3D5686BC16876EC038442FF98DC1"),
            ("BookOfEternityClient/Services/ShiningTradeRequestState.cs", "D24FC24C1CAF0E8C84088DB3AB507BC9E34537A97EE4E42DC3F7F0AE2E1A992E"),
            ("BookOfEternityClient/Services/SourceOfLightCapstoneState.cs", "E24F7A29B56C3342DA93370E90D387555F63F7C6BC1476D63029FB6603778164"),
            ("BookOfEternityClient/Services/SpiritualWoundCaptureCheckpointState.cs", "CD0FB7B5EB34C2F6D60973CE2EE2C56C1C50ACA2797438B9613B6C576A2B7EEE"),
            ("BookOfEternityClient/Services/SpiritualWoundDecisionPendingState.cs", "6FFCACA917ACC7FFBA066EBC9440EE0FEE08C230F869A3C0C140B82EBFB58DC7"),
            ("BookOfEternityClient/Services/SpiritualWoundOpportunityReceiptState.cs", "891DD804D226782D86CAB7C7A81D739EAFF2BB1366730D2E64913187549D7608"),
            ("BookOfEternityClient/Services/SystemGuardianLibraryService.cs", "74CAB4A0350770FCBEB1B1D783C3BCA603BAB7CB78660829DF902E1C4BE5F1A5"),
            ("BookOfEternityClient/Services/TrainingRequestState.cs", "524B30A6C574B22C19274120E45D55683B3BED98163107C10E5A314F334F1DF8"),
            ("BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs", "F6CE6B4767D130BB8F71C7A77742CB00AEAD9A3BF4525352203C9E085C164918"),
            ("BookOfEternityClient/Services/Validation/ValidationService.GuardiansAndAfterlife.cs", "0CC60E658442A70540C489FAE2E0E69644CA1AAE9D8DAE987232899C95C0B6AE"),
            ("BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs", "D9A268C30CB219FC1E66E3A6466880E4124155CBEFCC448A8B4824FDCB39179B"),
            ("BookOfEternityClient/Services/Validation/ValidationService.MetaControlShapeReadPhase.cs", "A0B1AF0D4B06A0D4C74BF31A1CFCA22FE9D902F3D7586B4D9A51888664AB7F93"),
            ("BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs", "B04EC022135F62FD7336156A340DCC37BF3AFEF05BD5D8A4F25D1921AE788F0E"),
            ("BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs", "6EC55FE70DECF80AC11A1146548A821F0AEC445B5132D5A7C683F49A1A4B6F86"),
            ("BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs", "54222A4E3EDE3574E4A489C88F8C085A015408DDEE4C6F9531ECF87D0D5AF907"),
            ("BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs", "96042BF2E790C80932810689D5DC0EE2F13ED567C2BE47487EFFA05C6730C0A0"),
            ("BookOfEternityClient/Services/Validation/ValidationService.SameTurnOwnerReadView.cs", "A8E0669C2BB1C692C2E9FA7481D78F2102267503BC31061974874DA9A4551971"),
            ("BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs", "081B4FD60653F4AC130B4598C232AB7CBA233EBEC17722D766DDC92461269B35"),
            ("BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs", "4452CEEB2069BC5340023B23AC9557F28CC78B84E4B5CF95B9D74819C4DE78CE"),
            ("BookOfEternityClient/Services/Validation/ValidationService.WorldReadPhase.cs", "7FF80990BFB94CE15C38389BE253D4618EADE091A1459339BD2D9873A82C6662"),
            ("BookOfEternityClient/Services/ValidationService.cs", "1DBC3F35E142D4F376B1EA4FCDF220D348B80BEE323FAE90E577D4CA5C2E9662"),
            ("BookOfEternityClient/Services/WorldDirectiveService.cs", "1E272E2A48AC49EB82894C917FA3476D24C568BFE9C903B358CB56FE3529C2C0"),
            ("BookOfEternityClient/Core/FileSystemManager.OrdinaryReadStreams.cs", "DD81A06EEA31A0F92EAC177D334336E37D34377E43FE71ADE7017B890855B989"),
            ("BookOfEternityClient/Core/FileSystemManager.TrustedLocalStorage.cs", "B9DB792399580E425CD7C7E521ADF79CCD6F2EB062F98896CAECE31CB6F55758"),
            ("BookOfEternityClient/Core/TrustedLocalFileScope.cs", "71D8C552AA7B2FEEB8B272B806841FBDDB87114D7C24D597EE97E2DA0A705131"),
            ("BookOfEternityClient/Services/SpiritualOriginalDraftInputs.cs", "23AB7EE1C59825D38232C99D1BC5082F874B9A7CC46547345E4D8E628569E972"),
            ("BookOfEternityClient/Services/AcceptedMechanicsPlan.cs", "5442EBFB6C21D1B0ED0148032EA74BE475EC846B5974F4A2DE75CB6FF51324B0"),
        ];
        foreach (var (path, expected) in pins)
            Assert.Equal(expected, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(TestRepoPaths.RepoRoot, path))).Replace("\r\n", "\n")))));
        var source = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/Services/Validation/ValidationService.MetaControlShapeReadPhase.cs"));
        foreach (var path in MetaShapePaths) Assert.Contains('"' + path + '"', source);
    }

    private static FileSystemManager.CanonicalWriteLease MetaShapePhaseLease(WorldReadProbe probe)
    {
        var phase = GetPrivateFieldValue<object?>(probe.Validator, "_metaControlShapeReadPhase");
        Assert.NotNull(phase);
        return Assert.IsType<FileSystemManager.CanonicalWriteLease>(phase.GetType()
            .GetProperty("Lease", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(phase));
    }

    private sealed class MetaOwningCloseProbe(WorldReadProbe probe, Exception? failure = null, Action? beforeClose = null) : IDisposable
    {
        internal int Calls, ProofsAtClose;
        public void Dispose()
        {
            Calls++;
            ProofsAtClose = probe.PhysicalProofSpans;
            beforeClose?.Invoke();
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void SeedMetaShapeOpeningCaches(WorldReadProbe probe, HashSet<string> ids, HashSet<string> names)
    {
        SeedWorldOpeningCaches(probe, ids, names);
        var seed = probe.OnOpened!;
        probe.OnOpened = () =>
        {
            var entered = MetaShapeContextRefs(probe.Validator);
            // Observation only: this callback does NOT assign an AsyncLocal or claim propagation.
            Assert.Null(entered[0]);
            Assert.Null(entered[1]);
            return seed();
        };
    }

    private static object?[] MetaShapeContextRefs(ValidationService validator)
    {
        object? Value(string name)
        {
            var local = GetPrivateFieldValue<object>(validator, name);
            return local.GetType().GetProperty("Value")!.GetValue(local);
        }
        return [Value("_gameStateValidationGuardianPolicyContextCache"),
            Value("_gameStateValidationGuardianProjectTrackerPolicyContextCache"),
            GetPrivateFieldValue<object?>(validator, "_guardianPolicyContextInProgress"), Value("_guardianMusingsPublication")];
    }
}
