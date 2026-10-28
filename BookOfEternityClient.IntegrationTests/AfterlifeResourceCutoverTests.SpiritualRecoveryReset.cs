using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Publishes real conflict re-trauma over a nonzero canonical recovery baseline,
    /// then starts the next safe cycle from that published reset.
    /// </summary>
    /// <returns>
    /// A task completing after C2/C4 reset, source-bound natural recovery and inert replay.
    /// The initial fixture progress is not a claim about an earlier recovery producer.
    /// </returns>
    [Fact]
    public async Task SpiritualRetraumaResetsRecoveryBeforeTheNextFreshSafeCycle()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: async original =>
            {
                await SeedOriginalIntakeBaselinesAsync(original);
                await SeedSpiritualWorseningTierAuthorityAsync(original);
                await SeedRecoveryResetOriginalAsync(original);
            },
            captureOriginalSnapshot: async original =>
            {
                var conflictControl = await new ProgressionScheduleService(original.FileSystem,
                    NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync();
                await using (var lease = await original.FileSystem.AcquireCanonicalWriteLeaseAsync())
                    Assert.Null(await AfterlifeWoundTreatmentAuthority.ProjectNaturalRecoveryAsync(original.FileSystem, lease,
                        new("session_resource_materialization", "request_resource_materialization", 42, conflictControl)));
                await original.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
                    preGeneratedDices1d20: [15, 5, 12, 8]);
            });
        var originalWound = await ReadRecoveryResetWoundAsync(context);
        Assert.Equal(1, originalWound.Severity.Rank);
        Assert.Equal(1, originalWound.Recovery.CurrentStepProgress);
        Assert.Null(originalWound.Care.ActiveCourseId);
        Assert.NotEqual("recovering", originalWound.Care.State);
        var priorTick = originalWound.Recovery.LastTickKey;
        var originalHistory = WoundHistoryState.Parse(
            (await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath).State!;
        var scheduler = new ProgressionScheduleService(context.FileSystem, NullLogger<ProgressionScheduleService>.Instance);

        // The normal first-pair issuer captures its own original; no decision,
        // receipt or completed plan is synthesized by this fixture.
        await CommitInitialC2PairAsync(context, candidate =>
        {
            var exchange = candidate["activeConflict"]!["exchangeLog"]![0]!;
            exchange["spiritualWoundTarget"] = new JsonObject
            {
                ["actorType"] = "guardian", ["actorId"] = "guardian_frame",
                ["retraumaWoundRef"] = RecoveryResetWoundId
            };
            exchange["after"]!["oppositionSideStrain"] = "fractured";
            candidate["activeConflict"]!["oppositionSideStrain"] = "fractured";
            foreach (var side in new[] { "player", "opposition" })
            {
                var cost = exchange["actionCostAudit"]![side]!;
                cost["artTier"] = 2; cost["effectiveCost"] = 1; cost["after"] = 5;
            }
            ResolveTerminalFixture(candidate);
        });
        const string scene = "Новый натиск углубил надлом воли хранителя.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
        { ["response"] = scene, ["timestamp"] = DateTime.UtcNow.ToString("O") }.ToJsonString());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            Assert.Equal("offer", opened.Disposition);
            using var offered = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            Assert.Equal(2, offered.Offer!.MinimumSeverityRank);
            var decision = JsonNode.Parse(CreateSpiritualWorseningLifecycleDecision(
                offered.Offer.OpportunityRef, worsening: true).GetRawText())!;
            var retained = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(originalWound))!;
            var classification = retained["classification"]!;
            decision["proposal"]!["classification"] = new JsonObject
            {
                ["woundType"] = classification["woundType"]!.DeepClone(),
                ["locationProfile"] = classification["locationProfile"]!.DeepClone()
            };
            var recovery = retained["recovery"]!.DeepClone().AsObject();
            recovery["currentStepProgress"] = 0;
            recovery["currentStepThreshold"] = 4; // The authored rank II has threshold 2 * II.
            recovery["blockers"] = JsonSerializer.SerializeToNode(
                WoundTransitionReducer.DeriveWorseningBlockers(originalWound));
            decision["proposal"]!["recovery"] = recovery;
            var submitted = await offered.SubmitDecisionAsync(lease,
                JsonSerializer.SerializeToElement(decision), scene);
            AssertNoConflictFrameErrors(submitted.Issues);
            Assert.Equal("completed_unpublished", submitted.Disposition);
            using var completed = submitted.Session;
            Assert.Equal(1, (await ReadRecoveryResetWoundAsync(context, lease)).Recovery.CurrentStepProgress);
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.NotNull(published.MechanicsPlan);
        Assert.Single(Assert.IsType<SpiritualWoundPublishedOutput>(published.SpiritualWoundOutput).Notifications);
        var reset = await ReadRecoveryResetWoundAsync(context);
        Assert.Equal(originalWound.WoundId, reset.WoundId);
        Assert.Equal(2, reset.Severity.Rank);
        Assert.Equal(0, reset.Recovery.CurrentStepProgress);
        Assert.Equal(priorTick, reset.Recovery.LastTickKey);
        Assert.Equal(originalWound.Origin, reset.Origin);
        var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!;
        Assert.Null(conflict["activeConflict"]);
        Assert.Single(conflict["recentConflicts"]!.AsArray());
        var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(),
            WoundHistoryState.HistoryPath).State!;
        Assert.Equal(new[] { "create", "worsen" }, history.Transitions.Select(t => t.Kind));
        Assert.Equal(originalHistory.Transitions[0], history.Transitions[0]);
        Assert.All(history.Transitions, row => { Assert.Null(row.AttemptId); Assert.NotEqual("recover", row.Kind); });
        var receipts = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.DeepClone();
        Assert.Single(receipts["decisions"]!.AsArray());
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        var publishedProfileBytes = await context.FileSystem.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath);
        var resourceRoots = new Dictionary<string, JsonNode?>();
        foreach (var path in new[] { ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                     ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath })
            resourceRoots.Add(path, (await context.ReadJsonAsync(path))!.DeepClone());

        // A new actual signed request captures the C4 publication, without
        // resetting history, resource bindings or scheduler state.
        var control = await scheduler.BuildControlForNextTurnAsync();
        JsonObject projection;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            projection = Assert.IsType<JsonObject>(await AfterlifeWoundTreatmentAuthority.ProjectNaturalRecoveryAsync(
                context.FileSystem, lease, new("session_recovery_reset", "request_recovery_reset_safe", 43, control)));
        var member = Assert.Single(projection["cohort"]!.AsArray())!;
        Assert.Equal(RecoveryResetWoundId, member["woundId"]!.GetValue<string>());
        Assert.Equal(0, member["healingTier"]!.GetValue<int>());
        Assert.Empty(projection["destinationGraphs"]!.AsArray());
        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "session_recovery_reset", RequestId = "request_recovery_reset_safe", TurnNumber = 43,
            CurrentRealm = "Chaos Sea", PlayerAction = "Спокойно наблюдать после завершённого конфликта.",
            ProgressionControl = control
        });
        var frame = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
            (await context.FileSystem.ReadFileAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!,
            LiveTurnPreparationService.ManifestJsonOptions)!;
        Assert.Equal(publishedProfileBytes, await context.FileSystem.ReadFileBytesAsync(frame.Files[AfterlifeEntityProfileState.StatePath]));
        await context.WriteExactJsonAsync(ProgressionScheduleService.ReportPath, JsonSerializer.Serialize(new
        {
            progressionProcessingReport = new
            {
                sessionId = "session_recovery_reset", requestId = "request_recovery_reset_safe", turnNumber = 43,
                worldCyclesProcessed = 0, factionCyclesProcessed = 0,
                chaosSeaCyclesProcessed = control.ChaosSeaCyclesExpectedThisTurn,
                guardianProjectCyclesProcessed = control.GuardianProjectCyclesExpectedThisTurn,
                residentAgencyCyclesProcessed = control.ResidentAgencyCyclesExpectedThisTurn,
                shiningAbodeCyclesProcessed = 0, shiningFactionCyclesProcessed = 0, shiningTradeCyclesProcessed = 0,
                newLastChaosSeaSimulationOrdinal = control.NextChaosSeaTurnOrdinal,
                newLastGuardianProjectCycleOrdinal = control.NextGuardianProjectCycleOrdinal,
                newLastResidentAgencyCycleOrdinal = control.NextResidentAgencyCycleOrdinal,
                newLastShiningAbodeCycleOrdinal = control.NextShiningAbodeCycleOrdinal,
                newLastShiningFactionCycleOrdinal = control.NextShiningFactionCycleOrdinal,
                newLastShiningTradeCycleOrdinal = control.NextShiningTradeCycleOrdinal,
                afterlifeCatchupProcessed = false, afterlifeCatchupSummaryEventsProcessed = 0
            }
        }));
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var issues = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
                .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            AssertNoConflictFrameErrors(issues);
            Assert.NotNull(await context.Normalizer.BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
        }
        var recovered = await ReadRecoveryResetWoundAsync(context);
        Assert.Equal(2, recovered.Severity.Rank);
        Assert.Equal(1, recovered.Recovery.CurrentStepProgress);
        Assert.NotEqual(priorTick, recovered.Recovery.LastTickKey);
        var finalHistory = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(),
            WoundHistoryState.HistoryPath).State!;
        Assert.Equal(new[] { "create", "worsen", "recover" }, finalHistory.Transitions.Select(t => t.Kind));
        Assert.Equal(history.Transitions.ToArray(), finalHistory.Transitions.Take(2).ToArray());
        Assert.Equal(43, finalHistory.Transitions[^1].Turn);
        Assert.All(finalHistory.Transitions, row => Assert.Null(row.AttemptId));
        var result = Assert.IsType<SpiritualHealingPersistedResult>(finalHistory.Transitions[^1].TransitionResult);
        Assert.Equal("natural", result.Mode);
        Assert.Null(result.ToCanonicalJson()["request"]);
        Assert.Equal(0, Assert.Single(result.Stages).Before.Recovery.CurrentStepProgress);
        Assert.True(JsonNode.DeepEquals(receipts, await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath)));
        foreach (var pair in resourceRoots)
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCycleCoreAsync(control, frame.SessionId,
                frame.RequestId, 43, path => context.FileSystem.ReadFileBytesAsync(lease, path),
                frame.ManifestPayloadHash, expectedNaturalProjection: projection));
        var profilesAfter = await context.FileSystem.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath);
        var historyAfter = await context.FileSystem.ReadFileBytesAsync(WoundHistoryState.HistoryPath);
        var scheduleAfter = await context.FileSystem.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath);
        Assert.Equal(control.NextChaosSeaTurnOrdinal, JsonNode.Parse(scheduleAfter!)!["currentChaosSeaTurnOrdinal"]!.GetValue<long>());
        AssertNoConflictFrameErrors(await scheduler.ValidateAcceptedTurnOutcomeAsync(control));
        await scheduler.ApplyAcceptedTurnOutcomeAsync(control);
        await scheduler.ApplyAcceptedTurnOutcomeAsync(control);
        Assert.Equal(profilesAfter, await context.FileSystem.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath));
        Assert.Equal(historyAfter, await context.FileSystem.ReadFileBytesAsync(WoundHistoryState.HistoryPath));
        Assert.Equal(scheduleAfter, await context.FileSystem.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath));
    }

    private const string RecoveryResetWoundId = "wound_recovery_before_conflict";

    private static async Task<WoundMaterializationEnvelope> ReadRecoveryResetWoundAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease? lease = null)
    {
        var json = lease is null ? await context.FileSystem.ReadFileAsync(AfterlifeEntityProfileState.StatePath)
            : await context.FileSystem.ReadFileAsync(lease, AfterlifeEntityProfileState.StatePath);
        var guardian = JsonNode.Parse(json!)!["profiles"]!.AsArray().Single(p => p!["actorId"]!.GetValue<string>() == "guardian_frame")!;
        var parsed = WoundMaterializationContract.Parse(Assert.Single(guardian["activeWounds"]!.AsArray())!.ToJsonString(), "reset.wound");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues));
        return parsed.Wound!;
    }

    private static async Task SeedRecoveryResetOriginalAsync(ResourceMaterializationTestContext context)
    {
        var raw = WoundContractTestData.CreateSpiritualActiveWound(RecoveryResetWoundId, "chaos_sea", "guardian",
            "guardian_frame", AfterlifeEntityProfileState.StatePath);
        raw["severity"]!["rank"] = 1; raw["severity"]!["value"] = "I"; raw["severity"]!["maximumAtCreation"] = "I";
        raw["severity"]!["lastChangeEventRef"] = "turn_41:wound_opened";
        raw["origin"]!["createdAtTurn"] = 41; raw["origin"]!["eventRef"] = "turn_41:wound_opened";
        raw["recovery"]!["currentStepThreshold"] = 2; raw["recovery"]!["currentStepProgress"] = 1;
        raw["recovery"]!["lastTickKey"] = "fixture_prior_safe_cycle";
        raw["lastTransition"]!["turn"] = 41; raw["lastTransition"]!["transitionId"] = "create_" + RecoveryResetWoundId;
        var consequences = raw["consequences"]!;
        consequences["slotBudget"] = 1; consequences["slotsUsed"] = 1;
        consequences["entries"]!.AsArray().RemoveAt(0); consequences["entries"]![0]!["slot"] = 1;
        consequences["ownedEffectSources"]!["definitions"]!.AsArray().RemoveAt(0);
        consequences["ownedEffectSources"]!["rootBindings"]!.AsArray().RemoveAt(0);
        var definition = consequences["ownedEffectSources"]!["definitions"]![0]!.AsObject();
        definition["components"]![0]!["payload"]!["operation"] = "guard";
        var parsed = WoundMaterializationContract.Parse(raw.ToJsonString(), "reset.original");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues));
        var wound = parsed.Wound!;
        var fp = WoundIdentityState.ComputeSemanticFingerprint(wound);
        var identities = WoundContractTestData.CreateIdentityIndex(WoundContractTestData.CreateIdentityEntry(RecoveryResetWoundId,
            "chaos_sea", "guardian", "guardian_frame", AfterlifeEntityProfileState.StatePath, "spiritual",
            createdAtTurn: 41, createdEventRef: wound.Origin.EventRef, semanticFingerprint: fp));
        var row = WoundContractTestData.CreateTransition();
        row["woundId"] = RecoveryResetWoundId; row["transitionId"] = wound.LastTransition.TransitionId;
        row["turn"] = 41; row["eventRef"] = wound.Origin.EventRef; row["operationKey"] = "create_" + RecoveryResetWoundId;
        row["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(RecoveryResetWoundId);
        row["afterFingerprint"] = fp; row["sourceFingerprint"] = "sha256:" + new string('e', 64); row["attemptId"] = null;
        row["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(row["operationKey"]!.GetValue<string>(),
            wound.Origin.EventRef, row["readableSummary"]!.GetValue<string>());
        var history = WoundContractTestData.CreateHistory(row);
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect("spiritual_action_cost_burden",
            "guardian", "chaos_sea", RecoveryResetWoundId);
        var binding = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
        effect["effectId"] = binding.EffectId; effect["target"]!["targetId"] = "guardian_frame";
        effect["source"]!["definitionKey"] = binding.DefinitionKey;
        foreach (var part in new[] { "display", "components", "triggers", "removal", "links" }) effect[part] = definition[part]!.DeepClone();
        effect["display"]!["sourceLabel"] = wound.Display.Name;
        effect["stacking"]!["stackKey"] = definition["stacking"]!["stackKey"]!.DeepClone();
        effect["chronology"]!["lastTransitionId"] = "seed_transition_" + binding.EffectId;
        effect["chronology"]!["createdEventRef"] = "seed_event_" + binding.EffectId;
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!;
        var guardian = profiles["profiles"]!.AsArray().Single(p => p!["actorId"]!.GetValue<string>() == "guardian_frame")!;
        guardian["activeWounds"] = new JsonArray(raw); guardian["activeEffects"] = new JsonArray(effect);
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        index["entries"]![0]!["owner"]!["carrierPath"] = AfterlifeEntityProfileState.StatePath;
        index["entries"]![0]!["transitions"]![0]!["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
        index["entries"]![0]!["transitions"]![0]!["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
        var catalog = WoundCarrierCatalog.Build(new(null, null, null, null, profiles.AsObject()));
        Assert.Empty(catalog.Issues);
        var identityState = WoundIdentityState.Parse(identities.ToJsonString(), WoundIdentityState.StatePath);
        var historyState = WoundHistoryState.Parse(history.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(identityState.IsValid); Assert.True(historyState.IsValid);
        Assert.Empty(historyState.State!.ValidateAgreement(identityState.State!, catalog));
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.WriteExactJsonAsync(WoundIdentityState.StatePath, identities.ToJsonString());
        await context.WriteExactJsonAsync(WoundHistoryState.HistoryPath, history.ToJsonString());
        await context.WriteExactJsonAsync(EffectIdentityState.StatePath, index.ToJsonString());
    }
}
