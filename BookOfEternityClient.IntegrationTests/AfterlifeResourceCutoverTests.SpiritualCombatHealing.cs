using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private const string CombatHealingWoundId = "wound_combat_healing";

    [Theory]
    [InlineData("healed")]
    [InlineData("no_improvement")]
    [InlineData("blocked_worsen")]
    [InlineData("next_boundary")]
    public Task CombatHealingUsesAnActualPairAndDurableOutcomeWithoutASafeCycle(string mode) =>
        RunCombatHealingPairAsync(mode, failPublication: false);

    [Fact]
    public Task CombatHealingBlockedGuaranteedCounterharmUsesItsOriginalPreimage() =>
        RunCombatHealingPairAsync("blocked_guaranteed", failPublication: false);

    private static bool IsBlockedCombat(string mode) => mode is "blocked_worsen" or "blocked_guaranteed";

    [Fact]
    public Task CombatHealingPublicationFailureRestoresItsActualSignedOriginal() =>
        RunCombatHealingPairAsync("healed", failPublication: true);

    private async Task RunCombatHealingPairAsync(string mode, bool failPublication)
    {
        var injectOriginalRead = false;
        var injectPublication = false;
        string? originalCommandPath = null;
        var uncertaintyCuts = 0;
        var publicationCuts = 0;
        var uncertainty = new CoordinatedStatePublicationUncertainException(new IOException("Combat original read uncertainty."));
        var publicationFailure = new IOException("Combat common publication write failure.");
        var hooks = new FileSystemManagerHooks
        {
            AfterCanonicalReadInitialValidationAsync = path =>
            {
                if (injectOriginalRead && path == originalCommandPath)
                {
                    uncertaintyCuts++;
                    throw uncertainty;
                }
                return Task.CompletedTask;
            },
            BeforeCanonicalMutationAsync = path =>
            {
                if (injectPublication && path == ResourceMaterializationContract.StatePath)
                {
                    injectPublication = false;
                    publicationCuts++;
                    throw publicationFailure;
                }
                return Task.CompletedTask;
            }
        };
        var tier = mode == "no_improvement" ? 1 : 3;
        var playerDie = mode is "healed" or "next_boundary" ? 19 : mode == "no_improvement" ? 2 : 5;
        var oppositionDie = IsBlockedCombat(mode) ? 15 : mode == "no_improvement" ? 2 : 5;
        ProgressionControl? control = null;
        JsonObject? intent = null;
        string? snapshotToken = null;
        await using var context = await CreateCompleteConflictFrameContextAsync(hooks,
            seedOriginalInputs: async original =>
            {
                await SeedOriginalIntakeBaselinesAsync(original);
                await SeedCombatHealingOriginalAsync(original, tier, rank: mode == "next_boundary" ? 2 : 1, guaranteedOpponent: mode == "blocked_guaranteed");
                // The old direct C2 fixture needs no world clock. Combat's
                // authenticated no-cycle basis does: use the same canonical
                // initial clock as the working full Engine counterpart, before
                // authority composition and this fixture's only snapshot.
                await original.WriteExactBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath,
                    await File.ReadAllBytesAsync(Path.Combine(TestRepoPaths.BaseSessionRoot,
                        EffectAcceptedTurnInputComposer.WorldTimePath)));
            },
            captureOriginalSnapshot: async original =>
            {
                var scheduler = new ProgressionScheduleService(original.FileSystem, NullLogger<ProgressionScheduleService>.Instance);
                await scheduler.EnsureInitializedAsync();
                Assert.True(ProgressionScheduleService.TryBuildSpiritualCombatControl(
                    (await original.ReadJsonAsync(ProgressionScheduleService.SchedulePath))!.AsObject(),
                    await original.FileSystem.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath), out _),
                    "Actual canonical world clock and settled zero-pending scheduler basis are required before staging.");
                var wound = await ReadCombatHealingWoundAsync(original);
                var conflict = (await original.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!.AsObject();
                var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
                { Combat = new(conflict["conflictId"]!.GetValue<string>(), SpiritualCombatHealingIntent.ConflictFingerprint(conflict), 1) };
                JsonObject projection;
                await using (var lease = await original.FileSystem.AcquireCanonicalWriteLeaseAsync())
                {
                    var staged = await AfterlifeWoundTreatmentAuthority.StagePlayerCombatTurnAsync(original.FileSystem, lease, scheduler,
                        "session_resource_materialization", "request_resource_materialization", 42, selection);
                    Assert.True(staged.Stage.IsStaged, staged.Stage.IssueCode);
                    control = Assert.IsType<ProgressionControl>(staged.Control);
                    projection = Assert.IsType<JsonObject>(staged.Stage.GmContext);
                    intent = JsonNode.Parse((await original.FileSystem.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
                }
                await original.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
                    preGeneratedDices1d20: [playerDie, oppositionDie, 12, 8], progressionControl: control, spiritualHealing: projection);
                var manifest = (await original.ReadJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!;
                Assert.Equal(PendingTurnSnapshotPathPresenceV1.CombatLogicalPaths.Order(StringComparer.Ordinal),
                    manifest["originalPathPresenceV1"]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal));
                Assert.False(manifest["originalPathPresenceV1"]![ProgressionScheduleService.ReportPath]!.GetValue<bool>());
                Assert.True(manifest["originalPathPresenceV1"]![EffectAcceptedTurnInputComposer.WorldTimePath]!.GetValue<bool>());
                await using (var originalLease = await original.FileSystem.AcquireCanonicalWriteLeaseAsync())
                {
                    var read = PendingTurnSnapshotReader.ReadObservedCurrent(original.FileSystem, originalLease,
                        PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                            new[] { AcceptedMechanicsPlan.WoundCommandPath }, AfterlifeWoundTreatmentAuthority.CombatSourcePaths));
                    Assert.True(read.Success, string.Join(";", read.Issues.Select(i => i.Code + ":" + i.Actual)));
                    var signedSources = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
                    Assert.Equal(AfterlifeWoundTreatmentAuthority.CombatSourcePaths.Order(StringComparer.Ordinal),
                        signedSources.CoveredLogicalPaths.Concat(signedSources.AbsentLogicalPaths)
                            .Where(p => p != AcceptedMechanicsPlan.WoundCommandPath).Order(StringComparer.Ordinal));
                    Assert.Contains(ProgressionScheduleService.ReportPath, signedSources.AbsentLogicalPaths);
                    read.Evidence!.RequireCurrent(original.FileSystem, originalLease, signedSources);
                }
                snapshotToken = manifest["manifestPayloadHash"]!.GetValue<string>();
                originalCommandPath = manifest["files"]![AcceptedMechanicsPlan.WoundCommandPath]!.GetValue<string>();
            });
        var rollbackImages = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        if (failPublication)
            foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(new[]
                { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                    SpiritualWoundOpportunityReceiptState.StatePath, "output/narrative_response.json" }).Distinct(StringComparer.Ordinal))
                rollbackImages.Add(path, await context.FileSystem.ReadFileBytesAsync(path));
        var before = await ReadCombatHealingWoundAsync(context);
        Assert.Equal(mode == "next_boundary" ? 2 : 1, before.Severity.Rank);
        Assert.Equal(1, before.Recovery.CurrentStepProgress);
        var originalTick = before.Recovery.LastTickKey;
        var schedule = await context.FileSystem.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath);
        var world = await context.FileSystem.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath);
        WoundHistoryState? preCombatHistory = null;
        Assert.Equal(control!.CurrentChaosSeaTurnOrdinal, control.NextChaosSeaTurnOrdinal);
        Assert.Equal(0, control.ChaosSeaCyclesExpectedThisTurn);

        await WriteCombatHealingExchangeAsync(context, mode, playerDie, oppositionDie, tier);
        await WriteOriginalIntakeDraftAsync(context);
        if (mode == "healed" && !failPublication)
        {
            // Pure response DATA is not execution permission. Establish the real
            // declaration shape, then alter one required audit at a time while
            // keeping the identical strain/control snapshots.
            var response = await ReadProjectedSourceContinuationCandidateAsync(context);
            var declared = response["recentConflicts"]![0]!["terminalExchange"]!.AsObject();
            var shapeIssues = new List<ValidationIssue>();
            ValidationService.ValidateRetainedSpiritualExchangeShape(declared, "declared healing", shapeIssues);
            AssertNoConflictFrameErrors(shapeIssues);
            for (var malformed = 0; malformed < 8; malformed++)
            {
                var changed = declared.DeepClone().AsObject();
                switch (malformed)
                {
                    case 0: changed["operationType"] = "pressure"; break;
                    case 1: changed["actionCostAudit"]!.AsObject().Remove("player"); break;
                    case 2:
                        changed["actionCostAudit"]!["player"]!["effectiveCost"] = 0;
                        changed["actionCostAudit"]!["player"]!["after"] = 6;
                        break;
                    case 3: changed["actionCostAudit"]!["player"]!["before"] = 1; break;
                    case 4: changed.Remove("diceAudit"); break;
                    case 5: changed["matchupAudit"]!["playerOperation"] = "pressure"; break;
                    case 6: changed["actionCostAudit"]!["player"]!["baseCost"] = 4; break;
                    case 7: changed["actionCostAudit"]!["player"]!["artTier"] = 6; break;
                }
                var refused = new List<ValidationIssue>();
                ValidationService.ValidateRetainedSpiritualExchangeShape(changed, "malformed healing", refused);
                Assert.Contains(refused, issue => issue.Code == "afterlife_conflict_exchange_no_state_delta");
            }
        }
        JsonObject? repairedInterface = null;
        if (mode == "next_boundary")
        {
            var graphs = AuthorCombatHealingGraphs(before, new JsonArray(new JsonObject
            {
                ["stageRef"] = SpiritualHealingRequestProjection.GraphRef(intent!["intentFingerprint"]!.GetValue<string>(), before.WoundId, 1),
                ["severityRank"] = 1, ["complications"] = SpiritualHealingRequestProjection.Complications(intent["intentFingerprint"]!.GetValue<string>(), before)
            }));
            var response = (await context.ReadJsonAsync("output/interface_updates.json")) ?? new JsonObject();
            response["spiritualHealingGraphs"] = graphs;
            repairedInterface = response.AsObject();
        }
        var scene = IsBlockedCombat(mode) ? "Встречный натиск углубил трещину духовной целостности души."
            : "Душа направила искусство исцеления на собственную трещину и завершила духовное противостояние.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
        { ["response"] = scene, ["timestamp"] = DateTime.UtcNow.ToString("O") }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        if (repairedInterface is not null)
        {
            await using var repairLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var originalRequest = await context.FileSystem.ReadFileBytesAsync(repairLease, AcceptedMechanicsPlan.WoundCommandPath);
            var originalHistory = await context.FileSystem.ReadFileBytesAsync(repairLease, WoundHistoryState.HistoryPath);
            var missing = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(repairLease);
            AssertNoConflictFrameErrors(missing.Issues);
            using (var capture = missing.Capture!)
            {
                AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(repairLease));
                var first = await capture.AdvanceNextResourceExchangeAsync(repairLease);
                AssertNoConflictFrameErrors(first.Issues);
                var refused = await capture.CommitC2FirstTransportAsync(repairLease, first.Step!.Interval!);
                Assert.Contains(refused.Issues, issue => issue.Code == "spiritual_combat_healing_graph_required");
            }
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(repairLease, SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(repairLease, SpiritualWoundDecisionPendingState.StatePath));
            Assert.Equal(originalHistory, await context.FileSystem.ReadFileBytesAsync(repairLease, WoundHistoryState.HistoryPath));
            Assert.Equal(originalRequest, await context.FileSystem.ReadFileBytesAsync(repairLease, AcceptedMechanicsPlan.WoundCommandPath));
            // Repair only GM response DATA, keeping the actual original intent/pool intact.
            await context.FileSystem.WriteFileAtomicAsync(repairLease, "output/interface_updates.json", repairedInterface.ToJsonString());
        }
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            if (mode == "healed" && !failPublication)
            {
                var signedIntent = SpiritualCombatHealingIntent.Parse(intent!);
                Assert.True(signedIntent.IsBound(new("session_resource_materialization",
                    "request_resource_materialization", 42, control!), signedIntent.Selection));
                Assert.False(signedIntent.IsBound(new("foreign_session",
                    "request_resource_materialization", 42, control!), signedIntent.Selection));
                var foreignControl = JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(control))!;
                foreignControl.NextChaosSeaTurnOrdinal++;
                Assert.False(signedIntent.IsBound(new("session_resource_materialization",
                    "request_resource_materialization", 42, foreignControl), signedIntent.Selection));
                // Wire classification is private to this actual original intake.
                // The ordinary GM command language still refuses the same object.
                var ordinary = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(intent));
                Assert.False(ordinary.Success);
                Assert.Contains(ordinary.Issues, issue => issue.Code == "wound_command_unknown_field");

                var exactCommand = (await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!;
                var changedCommand = exactCommand.Concat(new byte[] { (byte)'\n' }).ToArray();
                var unchanged = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
                foreach (var path in new[] { WoundHistoryState.HistoryPath, WoundIdentityState.StatePath,
                             AfterlifeEntityProfileState.StatePath, ValidationService.SpiritualWoundSourceSession.SoulPath,
                             EffectAcceptedTurnPlan.IdentityIndexPath, ResourceMaterializationContract.DefinitionsPath,
                             ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                             CanonicalResourceOwnerAuthorityComposer.AuthorityPath, ProgressionScheduleService.SchedulePath,
                             EffectAcceptedTurnInputComposer.WorldTimePath, SpiritualWoundCaptureCheckpointState.StatePath,
                             SpiritualWoundDecisionPendingState.StatePath }.Distinct(StringComparer.Ordinal))
                    unchanged.Add(path, await context.FileSystem.ReadFileBytesAsync(lease, path));
                await context.FileSystem.WriteFileAtomicBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath, changedCommand);
                try
                {
                    // Semantically identical live JSON is insufficient: its bytes
                    // differ from the actual signed client marker. No capture or
                    // first pair can be issued, and the generic parser must run.
                    var refused = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
                    using var refusedCapture = refused.Capture;
                    Assert.Null(refusedCapture);
                    Assert.Contains(refused.Issues, issue => issue.Code == "wound_command_unknown_field");
                    Assert.Equal(changedCommand, await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                    foreach (var pair in unchanged)
                        Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
                }
                finally
                {
                    // Restore the exact owned fixture input, before the independent
                    // normal capture below; no signed snapshot is rebuilt.
                    await context.FileSystem.WriteFileAtomicBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath, exactCommand);
                }
            }
            var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(recorded.Issues);
            using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var step = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(step.Issues);
            var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(step.Step?.Interval);
            // Read the current C1 candidate before the first pair writes its
            // private successors; the original capture is not a postcommit reader.
            var seedHistory = (await context.FileSystem.ReadFileBytesAsync(lease, WoundHistoryState.HistoryPath))!;
            var seedHistoryState = WoundHistoryState.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(seedHistory), WoundHistoryState.HistoryPath);
            Assert.True(seedHistoryState.IsValid, string.Join(";", seedHistoryState.Issues));
            preCombatHistory = seedHistoryState.State;
            var candidateImages = await capture.ReadC1CandidateOwnerImagesAsync(lease, interval);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(seedHistory)),
                JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(candidateImages[WoundHistoryState.HistoryPath].Bytes!))));
            string? expectedGuaranteeExchange = null;
            string? expectedGuaranteeArt = null;
            if (mode == "blocked_guaranteed")
            {
                var rawConflict = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)!;
                expectedGuaranteeExchange = rawConflict["recentConflicts"]![0]!["terminalExchange"]!.ToJsonString();
                var originalProfiles = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, AfterlifeEntityProfileState.StatePath))!)!;
                var oppositionProfile = Assert.Single(originalProfiles["profiles"]!.AsArray(), p =>
                    p!["actorId"]!.GetValue<string>() == "guardian_frame")!;
                expectedGuaranteeArt = Assert.Single(oppositionProfile["specialArts"]!.AsArray(), art =>
                    art!["artId"]!.GetValue<string>() == "art_source_owner")!.ToJsonString();
                Assert.NotEqual(SpiritualWoundStateJson.Canonical(SpiritualWoundStateJson.Parse(expectedGuaranteeExchange)), expectedGuaranteeExchange);
                Assert.NotEqual(SpiritualWoundStateJson.Canonical(SpiritualWoundStateJson.Parse(expectedGuaranteeArt)), expectedGuaranteeArt);
            }
            var first = await capture.CommitC2FirstTransportAsync(lease, interval);
            AssertNoConflictFrameErrors(first.Issues); Assert.Equal("committed", first.Disposition);
            var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(first.Pending!))!;
            Assert.Equal(2, packet["schemaVersion"]!.GetValue<int>());
            Assert.Single(packet["pending"]!["healingSources"]!.AsArray());
            Assert.Empty(packet["pending"]!["healingDecisions"]!.AsArray());
            Assert.Equal(0, packet["pending"]!["cursor"]!["nextHealingSourceOrdinal"]!.GetValue<int>());
            // The real first-pair producer requires WoundReadVersion0 and only
            // publishes the pair; its physical History is still the exact seed.
            Assert.Equal(seedHistory, await context.FileSystem.ReadFileBytesAsync(lease, WoundHistoryState.HistoryPath));
            if (mode == "blocked_guaranteed")
            {
                var harm = Assert.Single(packet["pending"]!["sources"]!.AsArray())!;
                Assert.Equal(2, harm["guaranteedSeverityRank"]!.GetValue<int>());
                var retained = SpiritualWoundSourceWitness.ReadGuaranteePreimage(harm.AsObject());
                Assert.Equal(expectedGuaranteeExchange, retained.ExchangeJson);
                Assert.Equal(expectedGuaranteeArt, retained.SpecialArtJson);
            }
            if (!IsBlockedCombat(mode)) Assert.Empty(packet["pending"]!["sources"]!.AsArray());
            capture.Dispose();
            var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
            if (mode == "healed" && !failPublication)
            {
                var pairBefore = new Dictionary<string, byte[]?>();
                foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                             WoundHistoryState.HistoryPath, ResourceMaterializationContract.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                    pairBefore.Add(path, await context.FileSystem.ReadFileBytesAsync(lease, path));
                injectOriginalRead = true;
                try
                {
                    var error = await Record.ExceptionAsync(() => adapter.OpenC2PrivateSessionAsync(lease));
                    Assert.Same(uncertainty, error);
                    Assert.Equal(1, uncertaintyCuts);
                }
                finally { injectOriginalRead = false; }
                foreach (var pair in pairBefore) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
                // Automatic first selection accepts no changed input, even a
                // semantically identical narrative encoding. Late corrections
                // belong only to a separately saved dependent continuation.
                var narrativeBefore = (await context.FileSystem.ReadFileBytesAsync(lease, ProjectionNarrativePath))!;
                await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath,
                    narrativeBefore.Concat(new byte[] { (byte)'\n' }).ToArray());
                try
                {
                    var changed = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
                        .OpenC2PrivateSessionAsync(lease);
                    using var refused = changed.Session;
                    Assert.Contains(changed.Issues, issue => issue.Code == "spiritual_c2_healing_automatic_input_changed");
                    foreach (var pair in pairBefore) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
                }
                finally { await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath, narrativeBefore); }
                // The normal positive uses a fresh adapter after removing the injected fault.
                adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
            }
            var opened = await adapter.OpenC2PrivateSessionAsync(lease);
            if (mode == "next_boundary")
                Assert.Contains(opened.Issues, issue => issue.Code == "afterlife_conflict_action_cost_mismatch");
            else AssertNoConflictFrameErrors(opened.Issues);
            if (mode == "next_boundary")
            {
                Assert.Equal("dependent_continuation", opened.Disposition);
                using var selected = opened.Session!;
                var checkpoint = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath))!)!;
                Assert.Equal("combat_healing", checkpoint["checkpoint"]!["pendingSubmission"]!["stagedDecision"]!["family"]!.GetValue<string>());
                Assert.Null(checkpoint["checkpoint"]!["pendingSubmission"]!["command"]);
                var retainedDecision = checkpoint["checkpoint"]!["pendingSubmission"]!["stagedDecision"]!.DeepClone();
                var dependent = await selected.ReadDependentContextAsync(lease);
                Assert.NotNull(dependent);
                Assert.Contains(dependent!.DependentDraftFields, field => field.JsonPointer.EndsWith("/actionCostAudit/player/effectiveCost", StringComparison.Ordinal));
                var current = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)!;
                current["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["player"]!["effectiveCost"] = 2;
                current["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["player"]!["after"] = 2;
                var policy = Assert.IsType<SpiritualWoundDependentDraftPolicy>(dependent.DraftPolicy);
                Assert.True(policy.Allows(current.AsObject()));
                foreach (var mutation in new Action<JsonObject>[]
                {
                    root => root["activeConflict"]!["exchangeLog"]![0]!["operationType"] = "guard",
                    root => root["activeConflict"]!["exchangeLog"]![0]!["diceAudit"]!["diceUsed"]![0]!["value"] = 1,
                    root => root["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["player"]!["baseCost"] = 1,
                    root => root["activeConflict"]!["unexpectedCorrection"] = true
                })
                {
                    var forbidden = current.DeepClone().AsObject(); mutation(forbidden);
                    Assert.False(policy.Allows(forbidden));
                }
                // The exact allowed correction is raw input, not an accepted afterimage.
                await context.FileSystem.WriteFileAtomicAsync(lease, AfterlifeSpiritualConflictState.StatePath, current.ToJsonString());
                var resumed = await selected.ResumeDependentContinuationAsync(lease);
                AssertNoConflictFrameErrors(resumed.Issues); Assert.Equal("completed_unpublished", resumed.Disposition); resumed.Session?.Dispose();
                Assert.Equal(retainedDecision["decisionFingerprint"]!.GetValue<string>(),
                    JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, SpiritualWoundDecisionPendingState.StatePath))!)!["pending"]!["healingDecisions"]![0]!["decisionFingerprint"]!.GetValue<string>());
            }
            else if (IsBlockedCombat(mode))
            {
                Assert.Equal("offer", opened.Disposition);
                using var offered = opened.Session!;
                var proposal = JsonNode.Parse(CreateSpiritualWorseningLifecycleDecision(offered.Offer!.OpportunityRef, true,
                    targetKind: "player").GetRawText())!;
                var retained = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(before))!;
                proposal["proposal"]!["classification"] = new JsonObject
                { ["woundType"] = retained["classification"]!["woundType"]!.DeepClone(), ["locationProfile"] = retained["classification"]!["locationProfile"]!.DeepClone() };
                var recovery = retained["recovery"]!.DeepClone(); recovery["currentStepProgress"] = 0; recovery["currentStepThreshold"] = 4;
                recovery["blockers"] = JsonSerializer.SerializeToNode(WoundTransitionReducer.DeriveWorseningBlockers(before));
                proposal["proposal"]!["recovery"] = recovery;
                proposal["proposal"]!["display"]!["acquisitionNarration"] = scene;
                var decided = await offered.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(proposal), scene);
                AssertNoConflictFrameErrors(decided.Issues); Assert.Equal("completed_unpublished", decided.Disposition); decided.Session?.Dispose();
            }
            else { Assert.Equal("completed_unpublished", opened.Disposition); opened.Session?.Dispose(); }
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(before), WoundMaterializationContract.SerializeCanonical(await ReadCombatHealingWoundAsync(context, lease)));
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        if (failPublication)
        {
            var backups = await ReadSpiritualC4BackupsAsync(context);
            injectPublication = true;
            Exception? failure;
            try
            {
                failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, context.Validator, backups));
            }
            finally { injectPublication = false; }
            Assert.Equal(1, publicationCuts);
            var writeFailure = Assert.IsType<CanonicalStateWriteException>(failure);
            Assert.Equal(ResourceMaterializationContract.StatePath, writeFailure.RelativePath);
            Assert.Same(publicationFailure, writeFailure.InnerException);
            foreach (var pair in rollbackImages) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            return;
        }
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues); Assert.NotNull(published.MechanicsPlan);
        var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath).State!;
        var receipts = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
        if (mode == "next_boundary")
        {
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnInterfacePayloadAsync());
            var response = (await context.ReadJsonAsync("output/interface_updates.json"))!.AsObject();
            response["dialogueOptions"] = new JsonArray("Продолжить духовное противостояние.");
            await context.WriteExactJsonAsync("output/interface_updates.json", response.ToJsonString());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnInterfacePayloadAsync());
            var acceptedResponse = await context.FileSystem.ReadFileBytesAsync("output/interface_updates.json");
            var acceptedMechanics = new Dictionary<string, byte[]?>();
            foreach (var path in new[] { WoundHistoryState.HistoryPath, WoundIdentityState.StatePath,
                         AfterlifeEntityProfileState.StatePath, ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath })
                acceptedMechanics.Add(path, await context.FileSystem.ReadFileBytesAsync(path));
            foreach (var change in new[] { "body", "reference" })
            {
                var altered = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(acceptedResponse!))!;
                var graph = altered["spiritualHealingGraphs"]![0]!;
                if (change == "reference") graph["stageRef"] = "foreign_combat_graph";
                else graph["consequenceDefinitions"]![0]!["root"]!["slots"]![0]!["readableSummary"] = "Подменённое ослабленное последствие.";
                await context.WriteExactJsonAsync("output/interface_updates.json", altered.ToJsonString());
                var issues = await context.Validator.ValidateAcceptedTurnInterfacePayloadAsync();
                Assert.Contains(issues, issue => issue.Code == "spiritual_healing_interface_unbound");
                foreach (var pair in acceptedMechanics) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
            }
            await context.FileSystem.WriteFileAtomicBytesAsync("output/interface_updates.json", acceptedResponse!);
        }
        if (mode == "next_boundary") Assert.NotNull((await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]);
        else Assert.Null((await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]);
        Assert.Single(receipts["healingSources"]!.AsArray()); Assert.Single(receipts["healingDecisions"]!.AsArray());
        Assert.Equal(0, receipts["healingDecisions"]![0]!["outcome"]!["waveOrdinal"]!.GetValue<int>());
        if (IsBlockedCombat(mode))
        {
            var wound = await ReadCombatHealingWoundAsync(context);
            Assert.Equal(2, wound.Severity.Rank); Assert.Equal(0, wound.Recovery.CurrentStepProgress); Assert.Equal(originalTick, wound.Recovery.LastTickKey);
            Assert.DoesNotContain(history.Transitions, t => t.TransitionResult is SpiritualCombatHealingPersistedResult);
            Assert.Equal(new[] { "create", "worsen" }, history.Transitions.Select(t => t.Kind));
            Assert.Equal(1, receipts["decisions"]![0]!["binding"]!["continuationGeneration"]!.GetValue<int>());
            Assert.Equal(0, receipts["decisions"]![0]!["binding"]!["waveOrdinal"]!.GetValue<int>());
            Assert.Null(receipts["healingDecisions"]![0]!["outcome"]!["result"]);
        }
        else
        {
            var result = Assert.Single(history.Transitions.Select(t => t.TransitionResult).OfType<SpiritualCombatHealingPersistedResult>());
            Assert.All(result.Stages, stage => Assert.Equal(originalTick, stage.After.Recovery.LastTickKey));
            if (mode is "healed" or "next_boundary") { Assert.Equal(new[] { "create", "treat", "heal" }, history.Transitions.Select(t => t.Kind)); Assert.Equal("healed", result.Stages[^1].After.Lifecycle); }
            else { Assert.Equal(new[] { "create", "treat" }, history.Transitions.Select(t => t.Kind)); Assert.Equal(1, (await ReadCombatHealingWoundAsync(context)).Recovery.CurrentStepProgress); }
        }
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCombatCoreAsync(control, "session_resource_materialization", "request_resource_materialization", 42,
                path => context.FileSystem.ReadFileBytesAsync(lease, path), snapshotToken, intent));
            if (IsBlockedCombat(mode))
            {
                var materialized = Assert.Single(history.Transitions, row => row.Kind == "worsen");
                var digest = receipts["decisions"]![0]!["sourceFingerprint"]!.GetValue<string>();
                Assert.NotEqual(materialized.SourceFingerprint, digest);
                var detached = WoundHistoryState.CreateValidated(history.NextOrdinal, history.Transitions.Select(row =>
                    row.TransitionId == materialized.TransitionId ? row with { SourceFingerprint = digest } : row));
                Assert.True(detached.IsValid);
                var detachedBytes = Encoding.UTF8.GetBytes(WoundHistoryState.SerializeCanonical(detached.State!));
                var parsed = WoundHistoryState.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(detachedBytes), WoundHistoryState.HistoryPath);
                Assert.True(parsed.IsValid); // A valid root; no chronology, ID or wound fingerprint changed.
                var identity = WoundIdentityState.Parse((await context.FileSystem.ReadFileAsync(lease, WoundIdentityState.StatePath))!, WoundIdentityState.StatePath);
                Assert.True(identity.IsValid);
                var roots = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
                foreach (var path in WoundAcceptedTurnSnapshotContract.CarrierPaths)
                {
                    var bytes = await context.FileSystem.ReadFileBytesAsync(lease, path);
                    roots.Add(path, bytes is null ? null : JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes))!.AsObject());
                }
                var catalog = WoundCarrierCatalog.Build(new(roots[WoundCarrierCatalog.PlayerPath], roots[WoundCarrierCatalog.NpcPath],
                    roots[WoundCarrierCatalog.EnemiesPath], roots[WoundCarrierCatalog.AlliesPath], roots[WoundCarrierCatalog.AfterlifeProfilesPath]));
                Assert.Empty(catalog.Issues);
                Assert.Empty(parsed.State!.ValidateAgreement(identity.State!, catalog));
                var physicalHistory = await context.FileSystem.ReadFileBytesAsync(lease, WoundHistoryState.HistoryPath);
                var refused = await Assert.ThrowsAsync<InvalidDataException>(() => ProgressionScheduleService.IsPublishedSpiritualCombatCoreAsync(control,
                    "session_resource_materialization", "request_resource_materialization", 42,
                    path => path == WoundHistoryState.HistoryPath ? Task.FromResult<byte[]?>(detachedBytes)
                        : context.FileSystem.ReadFileBytesAsync(lease, path), snapshotToken, intent));
                Assert.Contains("exact independently receipted counterharm", refused.Message);
                Assert.Equal(physicalHistory, await context.FileSystem.ReadFileBytesAsync(lease, WoundHistoryState.HistoryPath));
            }
            if (mode == "blocked_guaranteed")
            {
                // A rehashed witness with equivalent canonical JSON is still
                // foreign to the original guarantee preimage used by History.
                var foreign = receipts.DeepClone().AsObject();
                var harm = foreign["sources"]![0]!["witness"]!.AsObject();
                var preimage = SpiritualWoundSourceWitness.ReadGuaranteePreimage(harm);
                var exchange = SpiritualWoundStateJson.Parse(preimage.ExchangeJson);
                var reordered = new JsonObject();
                foreach (var field in exchange.Reverse()) reordered.Add(field.Key, field.Value?.DeepClone());
                Assert.NotEqual(preimage.ExchangeJson, reordered.ToJsonString());
                Assert.Equal(SpiritualWoundStateJson.Canonical(exchange), SpiritualWoundStateJson.Canonical(reordered));
                harm["guaranteePreimage"]!["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(reordered.ToJsonString()));
                var fingerprint = SpiritualWoundStateJson.Hash(harm, "source", "sourceId", "sourceFingerprint");
                harm["sourceId"] = "spiritual_source_" + fingerprint[7..];
                harm["sourceFingerprint"] = fingerprint;
                var decision = foreign["decisions"]![0]!.AsObject();
                decision["sourceId"] = harm["sourceId"]!.DeepClone();
                decision["sourceFingerprint"] = fingerprint;
                decision["sourceWitness"] = harm.DeepClone();
                decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
                var foreignJson = foreign.ToJsonString();
                var parsed = SpiritualWoundOpportunityReceiptState.Parse(foreignJson, SpiritualWoundOpportunityReceiptState.StatePath);
                Assert.True(parsed.IsValid, string.Join(";", parsed.Issues));
                var physicalReceipt = await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundOpportunityReceiptState.StatePath);
                var refused = await Assert.ThrowsAsync<InvalidDataException>(() => ProgressionScheduleService.IsPublishedSpiritualCombatCoreAsync(control,
                    "session_resource_materialization", "request_resource_materialization", 42,
                    path => path == SpiritualWoundOpportunityReceiptState.StatePath ? Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes(foreignJson))
                        : context.FileSystem.ReadFileBytesAsync(lease, path), snapshotToken, intent));
                Assert.Contains("exact independently receipted counterharm", refused.Message);
                Assert.Equal(physicalReceipt, await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundOpportunityReceiptState.StatePath));
            }
        }
        var schedulerAfter = new ProgressionScheduleService(context.FileSystem, NullLogger<ProgressionScheduleService>.Instance);
        AssertNoConflictFrameErrors(await schedulerAfter.ValidateAcceptedTurnOutcomeAsync(control));
        await schedulerAfter.ApplyAcceptedTurnOutcomeAsync(control); await schedulerAfter.ApplyAcceptedTurnOutcomeAsync(control);
        Assert.Equal(schedule, await context.FileSystem.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath));
        Assert.Equal(world, await context.FileSystem.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath));
        var complete = SpiritualWoundOpportunityReceiptState.Parse(receipts.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(complete.IsValid);
        if (IsBlockedCombat(mode)) AssertLaterInjuryCannotReuseHealingWave(receipts.AsObject());
        var omitted = receipts.DeepClone(); omitted["healingDecisions"]!.AsArray().Clear(); omitted["nextHealingDecisionOrdinal"] = 1;
        Assert.False(SpiritualWoundOpportunityReceiptState.Parse(omitted.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath).IsValid);
        var definitions = ResourceDefinitionCatalog.ParseCanonical((await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!.ToJsonString(), allowMissingPristine: false).Catalog!;
        var resourceHistory = ResourceHistoryState.ParseCanonical((await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath))!.ToJsonString(), definitions, allowMissingPristine: false).History!;
        complete.State!.RequireCombatHistoryResourceAgreement(history, resourceHistory);
        var familyOmitted = receipts.DeepClone().AsObject();
        foreach (var field in new[] { "healingSources", "healingDecisions", "nextHealingSourceOrdinal", "nextHealingDecisionOrdinal" }) familyOmitted.Remove(field);
        familyOmitted["schemaVersion"] = 1;
        var noFamily = SpiritualWoundOpportunityReceiptState.Parse(familyOmitted.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(noFamily.IsValid); // Both are current closed forms; this is a cross-root omission.
        if (!IsBlockedCombat(mode))
        {
            var error = Assert.Throws<FormatException>(() => noFamily.State!.RequireCombatHistoryResourceAgreement(history, resourceHistory));
            Assert.Contains("mandatory durable healing family", error.Message);
            var removedResult = (await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.DeepClone();
            Assert.Single(removedResult["transitions"]!.AsArray().OfType<JsonObject>(), row =>
                row["transitionResult"]?["kind"]?.GetValue<string>() == "spiritual_combat_healing").Remove("transitionResult");
            var withoutResult = WoundHistoryState.Parse(removedResult.ToJsonString(), WoundHistoryState.HistoryPath);
            Assert.False(withoutResult.IsValid); // Attempted treat requires its sealed result intrinsically.
            Assert.Contains(withoutResult.Issues, issue => issue.Code == "wound_history_treatment_result_missing");
            // The real valid History prefix is an independent cross-root
            // omission control; no attempt, row, fingerprint or identity is forged.
            Assert.NotNull(preCombatHistory);
            var missing = Assert.Throws<FormatException>(() => complete.State.RequireCombatHistoryResourceAgreement(preCombatHistory!, resourceHistory));
            Assert.Contains("actual wound History result", missing.Message);
        }
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var refused = await Assert.ThrowsAsync<InvalidDataException>(() => ProgressionScheduleService.IsPublishedSpiritualCombatCoreAsync(control,
                "session_resource_materialization", "request_resource_materialization", 42,
                path => path == SpiritualWoundOpportunityReceiptState.StatePath
                    ? Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes(familyOmitted.ToJsonString()))
                    : context.FileSystem.ReadFileBytesAsync(lease, path), snapshotToken, intent));
            Assert.Contains(IsBlockedCombat(mode) ? "mandatory accepted healing outcome" : "mandatory durable healing family", refused.Message);
        }
        var witness = receipts["healingSources"]![0]!["witness"]!.DeepClone().AsObject();
        _ = SpiritualCombatHealingPersistedResult.ReadSource(witness); // Valid standalone baseline first.
        foreach (var marker in new[] { "discarded", "unknown_marker" })
        {
            var forged = witness.DeepClone().AsObject();
            forged["diceClaim"]![0]!["selection"] = marker;
            forged["exchange"]!["diceAudit"]!["diceUsed"]![0]!["selection"] = marker;
            RehashCombatWitness(forged);
            var refusal = Assert.Throws<InvalidDataException>(() => SpiritualCombatHealingPersistedResult.ReadSource(forged));
            Assert.Contains("select exactly one die", refusal.Message);
        }
        // Standalone closed DATA consistency: internally recomputed calculation
        // and hashes cannot make a discarded larger die the admitted winner.
        var wrongWinner = witness.DeepClone().AsObject();
        wrongWinner["rollMode"] = "advantage";
        wrongWinner["healingSource"]!["RollMode"] = "advantage";
        wrongWinner["healingSource"]!["RollContributions"] = new JsonArray(new JsonObject
        { ["EffectId"] = "effect_advantage_data", ["ComponentId"] = "component_advantage_data", ["Contribution"] = "advantage" });
        wrongWinner["diceClaim"] = new JsonArray(new JsonObject
        { ["sourceIndex"] = 0, ["value"] = 1, ["selection"] = "selected" }, new JsonObject
        { ["sourceIndex"] = 2, ["value"] = 20, ["selection"] = "discarded" });
        var firstRow = wrongWinner["exchange"]!["diceAudit"]!["diceUsed"]![0]!.DeepClone();
        firstRow["sourceIndex"] = 0; firstRow["value"] = 1; firstRow["selection"] = "selected";
        var secondRow = firstRow.DeepClone(); secondRow["sourceIndex"] = 2; secondRow["value"] = 20; secondRow["selection"] = "discarded";
        wrongWinner["exchange"]!["diceAudit"]!["diceUsed"] = new JsonArray(firstRow, secondRow);
        wrongWinner["exchange"]!["diceAudit"]!["rollMode"] = new JsonObject { ["player"] = new JsonObject { ["effectiveMode"] = "advantage" } };
        var prepared = wrongWinner["healingSource"]!.Deserialize<SpiritualHealingPreparedSource>()!;
        wrongWinner["calculation"] = JsonSerializer.SerializeToNode(SpiritualHealingResolver.ResolvePrepared(prepared, "advantage", new[] { 1, 20 }).Resolution!.Calculation);
        RehashCombatWitness(wrongWinner);
        var correctWinner = wrongWinner.DeepClone().AsObject();
        foreach (var rows in new[] { correctWinner["diceClaim"]!.AsArray(), correctWinner["exchange"]!["diceAudit"]!["diceUsed"]!.AsArray() })
        {
            rows[0]!["selection"] = "discarded"; rows[1]!["selection"] = "selected";
        }
        RehashCombatWitness(correctWinner);
        _ = SpiritualCombatHealingPersistedResult.ReadSource(correctWinner);
        Assert.Contains("die selected", Assert.Throws<InvalidDataException>(() => SpiritualCombatHealingPersistedResult.ReadSource(wrongWinner)).Message);
    }

    private static void AssertLaterInjuryCannotReuseHealingWave(JsonObject accepted)
    {
        // Start from real accepted healing+counterharm, then build a detached
        // later-exchange DATA contract. This is not a publication/issuer proof.
        var later = accepted.DeepClone().AsObject();
        var source = later["sources"]![0]!["witness"]!.AsObject();
        var evidence = source["turnEvidence"]!.AsObject();
        var healingEvidence = later["healingSources"]![0]!["turnEvidence"]!;
        foreach (var current in new[] { evidence, healingEvidence })
        {
            current["bounds"]!["exchangeCount"] = 2; current["bounds"]!["sourceSlotCount"] = 4;
            current["acceptedD20Values"]![2] = 5; current["acceptedD20Values"]![3] = 15;
        }
        source["exchangeOrdinal"] = 1; source["sourceOrdinal"] = 1; source["exchangeId"] = "later_ordinary_harm_data";
        var exchange = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(source["exchangeJsonBase64"]!.GetValue<string>())))!;
        exchange["exchangeId"] = source["exchangeId"]!.DeepClone();
        foreach (var row in exchange["diceAudit"]!["diceUsed"]!.AsArray()) row!["sourceIndex"] = row["sourceIndex"]!.GetValue<int>() + 2;
        source["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(exchange.ToJsonString()));
        if (source["guaranteePreimage"] is JsonObject preimage)
        {
            var rawExchange = SpiritualWoundStateJson.Parse(SpiritualWoundSourceWitness.ReadGuaranteePreimage(source).ExchangeJson);
            rawExchange["exchangeId"] = source["exchangeId"]!.DeepClone();
            foreach (var row in rawExchange["diceAudit"]!["diceUsed"]!.AsArray()) row!["sourceIndex"] = row["sourceIndex"]!.GetValue<int>() + 2;
            preimage["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawExchange.ToJsonString()));
        }
        foreach (var row in source["selectedDiceIndices"]!.AsArray().Select((value, index) => (value, index)).ToArray())
            source["selectedDiceIndices"]![row.index] = row.value!.GetValue<int>() + 2;
        foreach (var claim in evidence["diceClaims"]!.AsArray())
        {
            claim!["exchangeOrdinal"] = 1; claim["sourceIndex"] = claim["sourceIndex"]!.GetValue<int>() + 2;
            claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim.AsObject());
        }
        var hash = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceId"] = "spiritual_source_" + hash[7..]; source["sourceFingerprint"] = hash;
        var decision = later["decisions"]![0]!.AsObject();
        decision["sourceId"] = source["sourceId"]!.DeepClone(); decision["sourceFingerprint"] = hash; decision["sourceWitness"] = source.DeepClone();
        decision["binding"]!["sourceOrdinal"] = 1; decision["binding"]!["waveOrdinal"] = 1; decision["binding"]!["continuationGeneration"] = 2;
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
        var valid = SpiritualWoundOpportunityReceiptState.Parse(later.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(valid.IsValid, string.Join(";", valid.Issues));
        decision["binding"]!["waveOrdinal"] = 0; decision["binding"]!["continuationGeneration"] = 1;
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
        var refused = SpiritualWoundOpportunityReceiptState.Parse(later.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.False(refused.IsValid);
        Assert.Contains(refused.Issues, issue => issue.Code == "spiritual_wound_receipt_invalid_state");
    }

    // This controlled GM authors only the I destination needed by these II
    // combat cases. It intentionally retains one legal guard-cost slot, rather
    // than assuming the natural fixture's original roll-hindrance profile exists.
    internal static JsonArray AuthorCombatHealingGraphs(WoundMaterializationEnvelope original, JsonArray requested)
    {
        var graphs = new JsonArray();
        foreach (var request in requested.OfType<JsonObject>())
        {
            Assert.Equal(1, request["severityRank"]!.GetValue<int>());
            var definition = original.Consequences.OwnedEffectSources.Definitions.Select(d => JsonNode.Parse(d.GetRawText())!.AsObject())
                .Single(d => d["components"]![0]!["profile"]?.GetValue<string>() == "spiritual_action_cost_burden" &&
                    d["components"]![0]!["payload"]!["operation"]?.GetValue<string>() == "guard");
            definition["links"] = new JsonArray();
            definition["components"]![0]!["payload"]!["magnitude"] = 1;
            graphs.Add(new JsonObject
            {
                ["stageRef"] = request["stageRef"]!.DeepClone(),
                ["consequenceDefinitions"] = new JsonArray(new JsonObject
                {
                    ["definitionRef"] = definition["definitionKey"]!.DeepClone(), ["definition"] = definition,
                    ["root"] = new JsonObject
                    {
                        ["ownership"] = new JsonObject { ["kind"] = "base_wound", ["complicationRef"] = null },
                        ["slots"] = new JsonArray(new JsonObject { ["profileKey"] = "spiritual_action_cost_burden",
                            ["readableSummary"] = "Ослабленная трещина ещё затрудняет духовную защиту." })
                    }
                })
            });
        }
        return graphs;
    }

    internal static async Task WriteCombatHealingExchangeAsync(ResourceMaterializationTestContext context, string mode,
        int playerDie, int oppositionDie, int tier)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!;
        var exchange = active["exchangeLog"]![0]!;
        exchange["operationType"] = AfterlifeActionCostRules.SpiritualHealingOperation;
        exchange["outcome"] = IsBlockedCombat(mode) ? "countered" : mode == "no_improvement" ? "partial_success" : "success";
        exchange["after"] = exchange["before"]!.DeepClone();
        active["oppositionSideStrain"] = "clear";
        exchange["matchupAudit"]!["playerOperation"] = AfterlifeActionCostRules.SpiritualHealingOperation;
        exchange["matchupAudit"]!["primaryResolutionLane"] = AfterlifeActionCostRules.SpiritualHealingOperation;
        exchange["matchupAudit"]!["riskProfile"] = "healing_timing";
        exchange["matchupAudit"]!["oppositionOperation"] = IsBlockedCombat(mode) ? "pressure" : "guard";
        var cost = Math.Max(2, 5 - tier);
        exchange["actionCostAudit"]!["player"] = new JsonObject
        { ["operationType"] = AfterlifeActionCostRules.SpiritualHealingOperation, ["baseCost"] = 5, ["minCost"] = 2,
            ["artTier"] = tier, ["effectiveCost"] = cost, ["before"] = 6, ["after"] = 6 - cost };
        exchange["actionCostAudit"]!["opposition"] = new JsonObject
        { ["operationType"] = IsBlockedCombat(mode) ? "pressure" : "guard", ["baseCost"] = IsBlockedCombat(mode) ? 3 : 2,
            ["minCost"] = 1, ["artTier"] = IsBlockedCombat(mode) ? 2 : 0,
            ["effectiveCost"] = IsBlockedCombat(mode) ? 1 : 2, ["before"] = 6, ["after"] = IsBlockedCombat(mode) ? 5 : 4 };
        if (mode == "blocked_guaranteed")
        {
            exchange["specialArtAudit"] = new JsonObject
            {
                ["artId"] = "art_source_owner", ["ownerActorType"] = "guardian", ["ownerActorId"] = "guardian_frame",
                ["baseOperation"] = "pressure", ["costMultiplierPercent"] = 200,
                ["effectNote"] = "Встречное искусство гарантирует углубление духовной трещины."
            };
            var costAudit = exchange["actionCostAudit"]!["opposition"]!;
            costAudit["specialArtId"] = "art_source_owner";
            costAudit["specialCostMultiplierPercent"] = 200;
            costAudit["standardEffectiveCost"] = 1;
            costAudit["effectiveCost"] = 2;
            costAudit["after"] = 4;
        }
        exchange["diceAudit"]!["diceUsed"]![0]!["side"] = "PLAYERSIDE";
        exchange["diceAudit"]!["diceUsed"]![0]!["value"] = playerDie;
        exchange["diceAudit"]!["diceUsed"]![0]!["selection"] = "selected";
        exchange["diceAudit"]!["diceUsed"]![1]!["value"] = oppositionDie;
        exchange["diceAudit"]!["playerTotal"] = playerDie;
        exchange["diceAudit"]!["oppositionTotal"] = oppositionDie;
        exchange["diceAudit"]!["margin"] = playerDie - oppositionDie;
        exchange["diceAudit"]!["outcomeBand"] = IsBlockedCombat(mode) ? "decisive_opposition_success"
            : mode == "no_improvement" ? "mixed_or_no_effect" : "decisive_player_success";
        if (IsBlockedCombat(mode))
        {
            exchange["after"]!["playerSideStrain"] = "fractured"; active["playerSideStrain"] = "fractured";
            exchange["incomingAction"] = new JsonObject
            {
                ["operationType"] = "pressure", ["spiritualWoundTarget"] = new JsonObject
                { ["actorType"] = "player", ["actorId"] = "player_soul", ["retraumaWoundRef"] = CombatHealingWoundId }
            };
        }
        if (mode == "next_boundary")
        {
            var next = exchange.DeepClone().AsObject(); next["exchangeId"] = "exchange_after_combat_heal";
            next["operationType"] = "guard"; next["outcome"] = "no_effect"; next.Remove("diceAudit");
            next["before"] = exchange["after"]!.DeepClone(); next["after"] = next["before"]!.DeepClone();
            next["matchupAudit"]!["playerOperation"] = "guard"; next["matchupAudit"]!["primaryResolutionLane"] = "guard";
            next["matchupAudit"]!["riskProfile"] = "safe_defense";
            next["actionCostAudit"]!["player"] = new JsonObject { ["operationType"] = "guard", ["baseCost"] = 2,
                ["minCost"] = 1, ["artTier"] = 0, ["effectiveCost"] = 3, ["before"] = 4, ["after"] = 1 };
            next["actionCostAudit"]!["opposition"] = new JsonObject { ["operationType"] = "guard", ["baseCost"] = 2,
                ["minCost"] = 1, ["artTier"] = 0, ["effectiveCost"] = 2, ["before"] = 4, ["after"] = 2 };
            active["exchangeLog"]!.AsArray().Add(next);
        }
        else
        {
            ResolveTerminalFixture(candidate);
            candidate["recentConflicts"]![0]!["operationType"] = AfterlifeActionCostRules.SpiritualHealingOperation;
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
    }

    private static async Task<WoundMaterializationEnvelope> ReadCombatHealingWoundAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease? lease = null)
    {
        var json = lease is null ? await context.FileSystem.ReadFileAsync(AfterlifeEntityProfileState.StatePath)
            : await context.FileSystem.ReadFileAsync(lease, AfterlifeEntityProfileState.StatePath);
        var player = JsonNode.Parse(json!)!["profiles"]!.AsArray().Single(p => p!["actorId"]!.GetValue<string>() == "player_soul")!;
        var parsed = WoundMaterializationContract.Parse(Assert.Single(player["activeWounds"]!.AsArray())!.ToJsonString(), "combat.fixture.wound");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues)); return parsed.Wound!;
    }

    internal static async Task SeedCombatHealingOriginalAsync(ResourceMaterializationTestContext context, int tier, int rank = 1, bool guaranteedOpponent = false)
    {
        await SeedSpiritualWorseningTierAuthorityAsync(context);
        var raw = WoundContractTestData.CreateSpiritualActiveWound(CombatHealingWoundId, "chaos_sea", "player_soul", "player_soul", AfterlifeEntityProfileState.StatePath);
        Assert.Contains(rank, new[] { 1, 2 });
        raw["severity"]!["rank"] = rank; raw["severity"]!["value"] = rank == 1 ? "I" : "II"; raw["severity"]!["maximumAtCreation"] = rank == 1 ? "I" : "II";
        raw["recovery"]!["currentStepThreshold"] = 2 * rank; raw["recovery"]!["currentStepProgress"] = 1;
        raw["recovery"]!["lastTickKey"] = "fixture_prior_safe_cycle";
        var consequences = raw["consequences"]!; consequences["slotBudget"] = rank; consequences["slotsUsed"] = rank;
        var definitions = consequences["ownedEffectSources"]!["definitions"]!.AsArray();
        if (rank == 1)
        {
            consequences["entries"]!.AsArray().RemoveAt(0); consequences["entries"]![0]!["slot"] = 1;
            definitions.RemoveAt(0); consequences["ownedEffectSources"]!["rootBindings"]!.AsArray().RemoveAt(0);
        }
        else
        {
            var old = definitions[0]!.AsObject(); var replacement = definitions[1]!.DeepClone().AsObject();
            replacement["definitionKey"] = old["definitionKey"]!.DeepClone(); replacement["stacking"] = old["stacking"]!.DeepClone();
            replacement["components"]![0]!["payload"]!["operation"] = "maneuver";
            definitions[0] = replacement;
            consequences["entries"]![0]!["profileKey"] = "spiritual_action_cost_burden";
        }
        definitions[^1]!["components"]![0]!["payload"]!["operation"] = "guard";
        var seed = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", raw);
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!;
        var player = profiles["profiles"]!.AsArray().Single(p => p!["actorId"]!.GetValue<string>() == "player_soul")!;
        if (guaranteedOpponent)
        {
            var opposition = profiles["profiles"]!.AsArray().Single(p => p!["actorId"]!.GetValue<string>() == "guardian_frame")!;
            var art = SourceOwnerSpecialArt("guardian", "guardian_frame");
            art["tier"] = 2;
            art["spiritualWoundEnvelope"] = new JsonObject
            { ["schemaVersion"] = 1, ["maximumSeverityRank"] = 2, ["guaranteedSeverityRank"] = 2 };
            opposition["specialArts"]!.AsArray().Add(art);
        }
        player["standardArts"]![AfterlifeSpiritualConflictState.SpiritualHealingArtId] = tier;
        player["activeWounds"] = seed.Carriers.AfterlifeProfiles!["profiles"]![0]!["activeWounds"]!.DeepClone();
        var wound = Assert.Single(WoundCarrierCatalog.Build(seed.Carriers).Occurrences).Wound;
        var effects = new List<JsonObject>();
        foreach (var binding in wound.Consequences.OwnedEffectSources.RootBindings)
        {
            var definition = definitions.OfType<JsonObject>().Single(d => d["definitionKey"]!.GetValue<string>() == binding.DefinitionKey);
            var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect("spiritual_action_cost_burden", "player", "chaos_sea", CombatHealingWoundId);
            effect["effectId"] = binding.EffectId; effect["target"]!["targetId"] = "player_soul";
            effect["source"]!["definitionKey"] = binding.DefinitionKey;
            foreach (var part in new[] { "display", "components", "triggers", "removal", "links" }) effect[part] = definition[part]!.DeepClone();
            effect["display"]!["sourceLabel"] = wound.Display.Name;
            effect["stacking"]!["stackKey"] = definition["stacking"]!["stackKey"]!.DeepClone();
            effect["chronology"]!["lastTransitionId"] = "seed_transition_" + binding.EffectId;
            effect["chronology"]!["createdEventRef"] = "seed_event_" + binding.EffectId;
            effects.Add(effect);
        }
        player["activeEffects"] = new JsonArray(effects.Select(e => (JsonNode)e.DeepClone()).ToArray());
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects.ToArray());
        foreach (var entry in index["entries"]!.AsArray().OfType<JsonObject>())
        {
            var effect = effects.Single(e => e["effectId"]!.GetValue<string>() == entry["effectId"]!.GetValue<string>());
            entry["owner"]!["carrierPath"] = AfterlifeEntityProfileState.StatePath;
            entry["transitions"]![0]!["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
            entry["transitions"]![0]!["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
        }
        var soul = (await context.ReadJsonAsync("game_state/meta/soul_state.json"))!;
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![AfterlifeSpiritualConflictState.SpiritualHealingArtId] = tier;
        AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthority(profiles.AsObject(), soul.AsObject(), null);
        var wounds = WoundCarrierCatalog.Build(seed.Carriers with { AfterlifeProfiles = profiles.AsObject() });
        Assert.Empty(wounds.Issues);
        Assert.Empty(seed.History.ValidateAgreement(seed.Identities, wounds));
        // These are detached preparation preconditions, not publication rights.
        // The real staging owner repeats its fresh source/lease fences later.
        var effectCatalog = EffectCarrierCatalog.Build(new(
            (await context.ReadJsonAsync(EffectCarrierCatalog.PlayerPath))?.AsObject(),
            (await context.ReadJsonAsync(EffectCarrierCatalog.NpcPath))?.AsObject(),
            (await context.ReadJsonAsync(EffectCarrierCatalog.EnemiesPath))?.AsObject(),
            (await context.ReadJsonAsync(EffectCarrierCatalog.AlliesPath))?.AsObject(), profiles.AsObject(),
            (await context.ReadJsonAsync(EffectCarrierCatalog.SpiritualConflictPath))?.AsObject()));
        var mechanics = EffectMechanicsSnapshot.Build(effectCatalog, index, null, out _);
        Assert.True(mechanics.IsAccepted, string.Join(";", mechanics.Issues.Select(i => i.Code + ":" + i.Actual)));
        var canonicalSources = EffectAcceptedTurnInputComposer.BuildCanonicalWoundSourceAuthority(wounds);
        Assert.Empty(canonicalSources.Issues);
        foreach (var binding in wound.Consequences.OwnedEffectSources.RootBindings)
        {
            Assert.True(effectCatalog.TryResolveOne(binding.EffectId, out var occurrence));
            var bound = canonicalSources.ResolveCanonicalBinding(new(wound.Owner.Realm, "wound", wound.WoundId, binding.DefinitionKey),
                occurrence!.Effect["target"]!["kind"]!.GetValue<string>());
            Assert.True(bound.Success);
            var issues = new List<ValidationIssue>();
            ValidationService.ValidateCanonicalSourceDefinitionAgreement(occurrence!, bound.Source!, canonicalSources, issues);
            AssertNoConflictFrameErrors(issues);
        }
        var prepared = SpiritualHealingResolver.PrepareSource(new(wound, wound.Owner, player.AsObject(), soul.AsObject(), mechanics));
        AssertNoConflictFrameErrors(prepared.Issues);
        Assert.Equal(tier, Assert.IsType<SpiritualHealingPreparedSource>(prepared.Source).HealingTier);
        await context.WriteExactJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.WriteExactJsonAsync(WoundIdentityState.StatePath, WoundIdentityState.SerializeCanonical(seed.Identities));
        await context.WriteExactJsonAsync(WoundHistoryState.HistoryPath, WoundHistoryState.SerializeCanonical(seed.History));
        await context.WriteExactJsonAsync(EffectIdentityState.StatePath, index.ToJsonString());
    }


    private static void RehashCombatWitness(JsonObject witness)
    {
        witness["exchangeFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            { WoundAcceptedTurnFingerprintWriter.CanonicalJson(witness["exchange"]!) });
        witness.Remove("sourceFingerprint");
        witness["sourceFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            { "book_of_eternity.spiritual_combat_healing.witness", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(witness) });
    }

}
