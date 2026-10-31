using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public async Task DiagnosisPurposeRetainsActualAcceptedClaimsAndRejectsSameIdTreatmentCollision()
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync();
        var owner = fixture.Authority;
        var before = owner.CurrentWound;
        var history = WoundHistoryState.Parse(await fixture.Files.ReadFileAsync(fixture.Lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid);
        var unchanged = fixture.CaptureJson();
        var ordinary = MortalWoundTreatmentPlanner.PrepareProcedureRequest(owner, history, before,
            "diagnosis_baseline_control", "inspect_explicit_method", fixture.EventRef);
        Assert.True(ordinary.IsValid, JsonSerializer.Serialize(ordinary.Issues));
        Assert.True(ordinary.Request!.RollbackNewProvisionalClaims(owner));
        var prepared = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, before,
            "diagnosis_same_operation", "inspect_explicit_method", fixture.EventRef);
        Assert.True(prepared.IsValid, JsonSerializer.Serialize(prepared.Issues));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(prepared.Request);
        Assert.Equal("diagnosis", request.Mode); Assert.Equal("diagnosis", request.Coordinates.Purpose);
        Assert.Equal("inspect_explicit_method", request.Coordinates.DiagnosisPathId); Assert.Empty(request.Coordinates.RouteId);
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority);
        Assert.Equal(new[] { 0 }, check.SourceIndices);
        var serialized = Assert.IsType<JsonObject>(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request));
        var serializedBefore = serialized.ToJsonString();
        var issues = new List<ValidationIssue>();
        Assert.True(MortalWoundTreatmentCommandCodec.TryParseRequest(serialized, "diagnosis.request", issues, out var detached),
            JsonSerializer.Serialize(issues));
        Assert.Equal(serializedBefore, serialized.ToJsonString());
        Assert.Equal(request.RequestFingerprint, detached!.RequestFingerprint);
        Assert.Equal("diagnosis", detached.Mode);
        Assert.Equal(request.Coordinates.DiagnosisPathId, detached.Coordinates.DiagnosisPathId);
        Assert.True(MortalWoundTreatmentDetachedSealValidator.IsValid(detached));
        var retry = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, before,
            "diagnosis_same_operation", "inspect_explicit_method", fixture.EventRef);
        Assert.True(retry.IsValid, JsonSerializer.Serialize(retry.Issues));
        Assert.Equal(request.RequestFingerprint, retry.Request!.RequestFingerprint);
        Assert.Equal(check.SourceIndices, Assert.IsType<MortalWoundProcedureCheckAuthority>(retry.Request.ModeAuthority).SourceIndices);
        var conflicting = MortalWoundTreatmentPlanner.PrepareProcedureRequest(owner, history, before,
            "diagnosis_same_operation", "inspect_explicit_method", fixture.EventRef);
        Assert.False(conflicting.IsValid); Assert.Null(conflicting.Request);
        var competitor = MortalWoundTreatmentPlanner.PrepareProcedureRequest(owner, history, before,
            "diagnosis_competing_operation", "inspect_explicit_method", fixture.EventRef);
        Assert.True(competitor.IsValid, JsonSerializer.Serialize(competitor.Issues));
        var competing = Assert.IsType<MortalWoundProcedureCheckAuthority>(competitor.Request!.ModeAuthority);
        Assert.Empty(check.SourceIndices.Intersect(competing.SourceIndices));
        Assert.Equal(new[] { 1 }, competing.SourceIndices);
        fixture.AssertJson(unchanged);
        Assert.DoesNotContain(history.State!.Transitions, transition => transition.Kind == "diagnose");
    }

    [Theory]
    [InlineData(20, "success")]
    [InlineData(1, "failed_attempt")]
    public async Task DiagnosisUsesRetainedProcedureOutcomeAndOnlyDeclaredKnowledge(int natural, string category)
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(natural);
        var owner = fixture.Authority; var before = owner.CurrentWound;
        var history = WoundHistoryState.Parse(await fixture.Files.ReadFileAsync(fixture.Lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        var unchanged = fixture.CaptureJson();
        var request = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, before,
            "diagnosis_terminal", "inspect_explicit_method", fixture.EventRef);
        Assert.True(request.IsValid, JsonSerializer.Serialize(request.Issues));
        var outcome = MortalWoundDiagnosisOutcome.Resolve(owner, history, request.Request!);
        Assert.Equal(category, outcome.Resolution.ResultCategory);
        Assert.Equal(natural, Assert.IsType<MortalWoundProcedureCheckAuthority>(request.Request!.ModeAuthority).NaturalRoll);
        Assert.Equal(category == "success", outcome.KnowledgeAfter.Treatment.KnownRouteIds.Contains("diagnostic_hidden_route"));
        Assert.Equal(before.Treatment.KnownRouteIds.Count + (category == "success" ? 1 : 0), outcome.KnowledgeAfter.Treatment.KnownRouteIds.Count);
        var normalized = outcome.KnowledgeAfter with { Treatment = before.Treatment, LastTransition = before.LastTransition };
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(before), WoundMaterializationContract.SerializeCanonical(normalized));
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(outcome.Resolution);
        Assert.True(finalization.IsValid, JsonSerializer.Serialize(finalization.Issues));
        Assert.Single(finalization.Finalization!.Consumptions);
        var retry = MortalWoundDiagnosisOutcome.Resolve(owner, history, request.Request!);
        Assert.Equal(outcome.Resolution.ResultFingerprint, retry.Resolution.ResultFingerprint);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(outcome.KnowledgeAfter), WoundMaterializationContract.SerializeCanonical(retry.KnowledgeAfter));
        fixture.AssertJson(unchanged);
    }

    [Fact]
    public async Task DiagnosisHoldRebindRestoresExactSpanBeforeACompetingClaimWithoutHistory()
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(signedInput: true);
        var before = fixture.Authority.CurrentWound;
        var history = WoundHistoryState.Parse(await fixture.Files.ReadFileAsync(fixture.Lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        var result = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(fixture.Authority, history, before,
            "diagnosis_retained_operation", "inspect_explicit_method", fixture.EventRef);
        Assert.True(result.IsValid, JsonSerializer.Serialize(result.Issues));
        var request = result.Request!;
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority);
        await fixture.Authority.RetainDiagnosisRequestAsync(request);
        var unchanged = fixture.CaptureJson();
        var outcome = MortalWoundDiagnosisOutcome.Resolve(fixture.Authority, history, request);
        await fixture.RebindAsync();
        var competitor = MortalWoundTreatmentPlanner.PrepareProcedureRequest(fixture.Authority, history, fixture.Authority.CurrentWound,
            "competing_after_rebind", "inspect_explicit_method", fixture.EventRef);
        Assert.True(competitor.IsValid, JsonSerializer.Serialize(competitor.Issues));
        var competing = Assert.IsType<MortalWoundProcedureCheckAuthority>(competitor.Request!.ModeAuthority);
        Assert.Equal(new[] { 1 }, competing.SourceIndices);
        var recovered = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(fixture.Authority, history, fixture.Authority.CurrentWound,
            "diagnosis_retained_operation", "inspect_explicit_method", fixture.EventRef);
        Assert.True(recovered.IsValid, JsonSerializer.Serialize(recovered.Issues));
        var exact = recovered.Request!;
        var restored = Assert.IsType<MortalWoundProcedureCheckAuthority>(exact.ModeAuthority);
        Assert.Equal(request.RequestFingerprint, exact.RequestFingerprint);
        Assert.Equal(check.SourceIndices, restored.SourceIndices); Assert.Equal(check.SourceRolls, restored.SourceRolls);
        Assert.Equal(request.ResourceAuthority.AuthorityFingerprint, exact.ResourceAuthority.AuthorityFingerprint);
        Assert.Equal(request.ResourceAuthority.ReservationId, exact.ResourceAuthority.ReservationId);
        Assert.Empty(restored.SourceIndices.Intersect(competing.SourceIndices));
        Assert.Equal(outcome.Resolution.ResultFingerprint, MortalWoundDiagnosisOutcome.Resolve(fixture.Authority, history, exact).Resolution.ResultFingerprint);
        Assert.DoesNotContain(history.State!.Transitions, value => value.Kind == "diagnose");
        fixture.AssertJson(unchanged);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(300)]
    public async Task DiagnosisWorkingTimeAndRecoveryUseOriginalSourcesWithoutFutureWrites(int duration)
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: duration);
        var owner = fixture.Authority;
        var history = WoundHistoryState.Parse(await fixture.Files.ReadFileAsync(fixture.Lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        var unchanged = fixture.CaptureJson();
        var prepared = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, owner.CurrentWound,
            "diagnosis_working_clock", "inspect_explicit_method", fixture.EventRef);
        Assert.True(prepared.IsValid, JsonSerializer.Serialize(prepared.Issues));
        var outcome = MortalWoundDiagnosisOutcome.Resolve(owner, history, prepared.Request!);
        var working = MortalWoundDiagnosisWorkingProjection.Build(owner, outcome);
        Assert.Equal(owner.CurrentGameMinute, working.OriginalSource.CurrentGameMinute);
        Assert.Equal(owner.CurrentGameMinute, working.OriginalMinute);
        Assert.Equal(checked(owner.CurrentGameMinute + duration), working.ResultingMinute);
        Assert.Equal(working.ResultingMinute, working.ClockAfter["currentTimeInMinutes"]!.GetValue<long>());
        var member = Assert.Single(working.Members);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(owner.CurrentWound), WoundMaterializationContract.SerializeCanonical(member.Original));
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(outcome.KnowledgeAfter), WoundMaterializationContract.SerializeCanonical(member.Working));
        Assert.Equal(working.ResultingMinute, member.Resolution.CurrentTimeInMinutes);
        Assert.Equal(duration / 10, member.Resolution.ElapsedCadences);
        Assert.Equal(0, member.Working.Recovery.CurrentStepProgress);
        var sourceClock = owner.ReadDiagnosisOriginalRoot(EffectAcceptedTurnInputComposer.WorldTimePath);
        sourceClock["currentTimeInMinutes"] = working.ResultingMinute;
        Assert.True(JsonNode.DeepEquals(sourceClock, working.ClockAfter));
        var escaped = working.ClockAfter; escaped["currentTimeInMinutes"] = 1;
        Assert.Equal(working.ResultingMinute, working.ClockAfter["currentTimeInMinutes"]!.GetValue<long>());
        var scheduler = new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance);
        var calculation = await scheduler.PrepareDiagnosisOutcomeAsync(fixture.Lease, working);
        var after = JsonNode.Parse(FileSystemManager.DecodeFileBytes(calculation.ScheduleAfter.Bytes!))!.AsObject();
        Assert.Equal(working.ResultingMinute, after["currentWorldTimeInMinutes"]!.GetValue<int>());
        var originalControl = working.OriginalControl;
        Assert.Equal(working.OriginalMinute, originalControl["currentWorldTimeInMinutes"]!.GetValue<int>());
        Assert.Equal(0, originalControl["worldCyclesAlreadyPendingBeforeTurn"]!.GetValue<int>());
        var cadence = originalControl["worldCycleMinutes"]!.GetValue<int>();
        Assert.Equal(duration / cadence, after["pendingWorldCycles"]!.GetValue<int>());
        Assert.False(calculation.ReportBefore.Existed); Assert.False(calculation.ReportAfter.Existed);
        fixture.AssertJson(unchanged);
        await fixture.RebindAsync();
        Assert.Throws<InvalidDataException>(() => working.RequireCurrent(fixture.Authority));
    }

    [Fact]
    public async Task DiagnosisDurableComparisonRetainsOriginalAndRefusesResealedSemanticDrift()
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 0);
        var owner = fixture.Authority;
        var history = WoundHistoryState.Parse(await fixture.Files.ReadFileAsync(fixture.Lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        var prepared = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, owner.CurrentWound,
            "diagnosis_durable_comparison", "inspect_explicit_method", fixture.EventRef);
        Assert.True(prepared.IsValid, JsonSerializer.Serialize(prepared.Issues));
        var outcome = MortalWoundDiagnosisOutcome.Resolve(owner, history, prepared.Request!);
        var working = MortalWoundDiagnosisWorkingProjection.Build(owner, outcome);
        var calculation = await new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance)
            .PrepareDiagnosisOutcomeAsync(fixture.Lease, working);
        var unchanged = fixture.CaptureJson();
        // Comparison DATA only. No effect publication, accepted receipt, live
        // result authority or History row is fabricated by this codec control.
        var result = MortalWoundDiagnosisPersistedResult.CreateComparisonEvidence(owner, outcome, working, calculation,
            working.Members.ToDictionary(value => value.Original.WoundId,
                _ => (IReadOnlyList<MortalWoundRecoveryPublishedStage>)Array.Empty<MortalWoundRecoveryPublishedStage>(), StringComparer.Ordinal));
        Assert.Equal("mortal_diagnosis", result.Kind);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(owner.CurrentWound), WoundMaterializationContract.SerializeCanonical(result.Original));
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(outcome.KnowledgeAfter), WoundMaterializationContract.SerializeCanonical(result.KnowledgeAfter));
        var root = result.ToCanonicalJson();
        var issues = new List<ValidationIssue>();
        Assert.NotNull(MortalWoundDiagnosisPersistedResult.Parse(JsonSerializer.SerializeToElement(root), "diagnosis.result", issues));
        Assert.Empty(issues);
        // Complete serialized comparison History is DATA, not a claim that this
        // component fixture performed a canonical publication.
        const string summary = "The declared Mortal diagnosis and elapsed recovery are retained for replay.";
        var coordinates = prepared.Request!.Coordinates;
        var knowledge = result.KnowledgeAfter;
        var row = new WoundHistoryTransition(knowledge.LastTransition.TransitionId, knowledge.WoundId,
            owner.History.NextOrdinal, knowledge.LastTransition.Ordinal, "diagnose", coordinates.Turn,
            coordinates.EventRef, coordinates.OperationKey,
            WoundIdentityState.ComputeSemanticFingerprint(result.Original), WoundIdentityState.ComputeSemanticFingerprint(knowledge),
            result.WorkingFingerprint, coordinates.AttemptId, null, null, null, null,
            WoundHistoryState.ComputeOutputFingerprint(coordinates.OperationKey, coordinates.EventRef, summary), summary, false, result);
        var complete = WoundHistoryState.CreateValidated(owner.History.NextOrdinal + 1, owner.History.Transitions.Append(row));
        Assert.True(complete.IsValid, JsonSerializer.Serialize(complete.Issues));
        var serializedHistory = WoundHistoryState.SerializeCanonical(complete.State!);
        var parsedHistory = WoundHistoryState.Parse(serializedHistory, WoundHistoryState.HistoryPath);
        Assert.True(parsedHistory.IsValid, JsonSerializer.Serialize(parsedHistory.Issues));
        var probe = new WoundHistoryReplayProbe(row.OperationKey, row.WoundId, row.Kind, row.Turn, row.EventRef,
            row.BeforeFingerprint, row.AfterFingerprint, row.SourceFingerprint, row.AttemptId, null, null, null, null,
            row.OutputFingerprint, row.ReadableSummary, row.Terminal, result);
        Assert.Equal(WoundHistoryReplayDisposition.Exact, parsedHistory.State!.ResolveReplay(probe).Disposition);
        Assert.Equal(WoundHistoryReplayDisposition.Conflict,
            parsedHistory.State.ResolveReplay(probe with { Kind = "treat" }).Disposition);
        Assert.Equal((0L, 0L), parsedHistory.State.GetRecoveryConsumption(knowledge));
        foreach (var fault in new[] { "prefix", "source", "duplicate", "result_omission" })
        {
            var changedHistory = JsonNode.Parse(serializedHistory)!.AsObject();
            var rows = changedHistory["transitions"]!.AsArray();
            var diagnosisRow = rows[^1]!.AsObject();
            switch (fault)
            {
                case "prefix":
                    var first = rows[0]!.AsObject();
                    first["readableSummary"] = "A different original accepted output.";
                    first["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(
                        first["operationKey"]!.GetValue<string>(), first["eventRef"]!.GetValue<string>(), first["readableSummary"]!.GetValue<string>());
                    break;
                case "source": diagnosisRow["sourceFingerprint"] = row.BeforeFingerprint; break;
                case "duplicate": rows.Add(diagnosisRow.DeepClone()); changedHistory["nextOrdinal"] = complete.State!.NextOrdinal + 1; break;
                case "result_omission": diagnosisRow["transitionResult"] = null; break;
            }
            var refused = WoundHistoryState.Parse(changedHistory.ToJsonString(), WoundHistoryState.HistoryPath);
            if (fault == "result_omission")
            {
                // Optional generic diagnose rows cannot reconstruct a retained
                // terminal request and therefore cannot supply exact replay.
                Assert.True(refused.IsValid, JsonSerializer.Serialize(refused.Issues));
                Assert.Equal(WoundHistoryReplayDisposition.Conflict, refused.State!.ResolveReplay(probe).Disposition);
            }
            else Assert.False(refused.IsValid);
        }
        foreach (var fault in new[] { "knowledge", "result", "duration", "control", "cohort", "extra", "resource" })
        {
            var changed = root.DeepClone().AsObject();
            switch (fault)
            {
                case "knowledge": changed["knowledgeAfter"]!["treatment"]!["knownRouteIds"]!.AsArray().RemoveAt(owner.CurrentWound.Treatment.KnownRouteIds.Count); break;
                case "result": changed["terminal"]!["category"] = "failed_attempt"; break;
                case "duration": changed["clock"]!["resultingMinute"] = working.ResultingMinute + 1; break;
                case "control":
                    changed["input"]!["control"]!["worldCycleMinutes"] = 1;
                    changed["input"]!["inputFingerprint"] = MortalWoundDiagnosisInputCodec.Fingerprint(changed["input"]!.AsObject());
                    break;
                case "cohort": changed["cohort"]!.AsArray().Clear(); break;
                case "extra": changed["terminal"]!["treatmentOperations"] = new JsonArray(); break;
                case "resource": changed["resourceFinalization"]!["consumptionTrigger"] = "none"; break;
            }
            changed["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(changed);
            var failure = new List<ValidationIssue>();
            Assert.Null(MortalWoundDiagnosisPersistedResult.Parse(JsonSerializer.SerializeToElement(changed), "diagnosis.result", failure));
            Assert.Contains(failure, value => value.Code == "mortal_wound_diagnosis_result_invalid");
        }
        Assert.True(JsonNode.DeepEquals(root, result.ToCanonicalJson()));
        fixture.AssertJson(unchanged);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(300)]
    public async Task DiagnosisExecutesKnowledgeBeforeOnlyActuallyDueRecoveryInExistingEffectOwner(int duration)
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: duration);
        var owner = fixture.Authority;
        var history = new WoundHistoryParseResult(owner.History, Array.Empty<ValidationIssue>());
        var request = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, owner.CurrentWound,
            "diagnosis_effect_execution", "inspect_explicit_method", fixture.EventRef);
        Assert.True(request.IsValid, JsonSerializer.Serialize(request.Issues));
        var outcome = MortalWoundDiagnosisOutcome.Resolve(owner, history, request.Request!);
        var working = MortalWoundDiagnosisWorkingProjection.Build(owner, outcome);
        var progression = await new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance)
            .PrepareDiagnosisOutcomeAsync(fixture.Lease, working);
        var originals = fixture.CaptureJson();
        var prepared = WoundAcceptedTurnPlanner.PrepareMortalDiagnosisContinuation(owner, outcome, working, progression);
        Assert.True(prepared.Success, JsonSerializer.Serialize(prepared.Issues));
        var effectInput = WoundAcceptedTurnPlanner.ComposeMortalDiagnosisEffectInput(prepared.Plan!);
        var effects = WoundEffectBatchPlanner.Build(prepared.Plan!, effectInput, new EffectIdentityFactory());
        Assert.True(effects.Success, JsonSerializer.Serialize(effects.Issues));
        Assert.True(EffectAcceptedTurnPlanner.TryReadMortalDiagnosisExecution(prepared.Plan!, effects.Plan!.EffectPlan, out var stages));
        var first = Assert.Single(stages, value => value.Kind == "diagnose");
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(outcome.KnowledgeAfter), WoundMaterializationContract.SerializeCanonical(first.After));
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(owner.CurrentWound), WoundMaterializationContract.SerializeCanonical(first.Before));
        if (duration == 0) Assert.Single(stages);
        else
        {
            Assert.True(stages.Count > 1);
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(first.After), WoundMaterializationContract.SerializeCanonical(stages[1].Before));
            Assert.All(stages.Skip(1), value => Assert.Contains("diagnostic_hidden_route", value.After.Treatment.KnownRouteIds));
        }
        Assert.Equal(working.ResultingMinute, effectInput.EventInput["currentTime"]!.GetValue<long>());
        var final = WoundAcceptedTurnPlanner.Finalize(prepared.Plan!, effects);
        Assert.True(final.Success, JsonSerializer.Serialize(final.Issues));
        var complete = WoundHistoryState.Parse(final.Plan!.HistoryAfterImage.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(complete.IsValid, JsonSerializer.Serialize(complete.Issues));
        Assert.Single(complete.State!.Transitions, value => value.TransitionResult is MortalWoundDiagnosisPersistedResult);
        Assert.Equal(owner.History.Transitions.Count + stages.Count, complete.State.Transitions.Count);
        fixture.AssertJson(originals);
    }

    [Fact]
    public async Task DiagnosisCommonPlanKeepsHeldAndOuterOriginalPendingImagesDistinct()
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 300);
        var owner = fixture.Authority;
        var originalPending = await fixture.Files.ReadFileBytesAsync(fixture.Lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        var request = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner,
            new(owner.History, Array.Empty<ValidationIssue>()), owner.CurrentWound,
            "diagnosis_common_publication", "inspect_explicit_method", fixture.EventRef);
        Assert.True(request.IsValid, JsonSerializer.Serialize(request.Issues));
        await owner.RetainDiagnosisRequestAsync(request.Request!);
        var heldPending = await fixture.Files.ReadFileBytesAsync(fixture.Lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        Assert.NotNull(heldPending);
        Assert.False(originalPending is not null && originalPending.AsSpan().SequenceEqual(heldPending));
        var staged = await fixture.Files.ReadFileBytesAsync(fixture.Lease, AcceptedMechanicsPlan.WoundCommandPath);
        var heldTree = fixture.CaptureJson();
        var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
        var locationIssues = await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease);
        Assert.Empty(locationIssues);
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
        var prepared = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(fixture.Files, fixture.Lease,
            new GameResponse(), owner, request.Request!);
        Assert.True(prepared.IsValid, JsonSerializer.Serialize(prepared.Issues));
        var plan = prepared.Plan!;
        Assert.NotNull(plan.TreatmentResourcePublicationAuthority);
        Assert.Equal("diagnosis", plan.TreatmentResourcePublicationAuthority!.RequestAuthority.Coordinates.Purpose);
        Assert.Equal(heldPending, plan.BeforeImages[WoundAcceptedTurnSnapshotContract.PendingResolutionPath].Bytes);
        Assert.Equal(staged, plan.BeforeImages[AcceptedMechanicsPlan.WoundCommandPath].Bytes);
        Assert.Contains(EffectAcceptedTurnInputComposer.WorldTimePath, plan.TouchedPaths);
        Assert.Contains(ProgressionScheduleService.SchedulePath, plan.TouchedPaths);
        Assert.Contains(WoundAcceptedTurnSnapshotContract.PendingResolutionPath, plan.ConsumedPaths);
        Assert.Contains(AcceptedMechanicsPlan.WoundCommandPath, plan.ConsumedPaths);
        Assert.Equal(owner.CurrentGameMinute, JsonNode.Parse(await fixture.Files.ReadFileAsync(fixture.Lease,
            EffectAcceptedTurnInputComposer.WorldTimePath))!["currentTimeInMinutes"]!.GetValue<long>());
        fixture.AssertJson(heldTree);
    }

    private sealed class DiagnosisOwnershipFixture : IAsyncDisposable
    {
        private DiagnosisOwnershipFixture(string root, FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalWoundTreatmentAcceptedStateAuthority authority, string eventRef)
        { Root = root; Files = files; Lease = lease; Authority = authority; EventRef = eventRef; }
        private string Root { get; }
        internal FileSystemManager Files { get; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; private set; }
        internal MortalWoundTreatmentAcceptedStateAuthority Authority { get; private set; }
        internal string EventRef { get; }

        internal static async Task<DiagnosisOwnershipFixture> CreateAsync(int? natural = null, bool signedInput = false, int duration = 30)
        {
            // Initial canonical scenario DATA only. The real live preparation and
            // exporter below issue ownership; these JSON values are not receipts.
            var scenario = CreateScenario("procedure_normal_uses_lowest_free_die", "procedure");
            if (natural is not null) scenario.AcceptedState["acceptedDice"] = new JsonArray(natural.Value, 17, 1, 20);
            var method = WoundContractTestData.CreateMortalDiagnosisPath(true, duration);
            var route = Assert.IsType<JsonObject>(Assert.Single(scenario.Before["treatment"]!["routes"]!.AsArray())).DeepClone().AsObject();
            route["routeId"] = "inspect_explicit_method";
            scenario.Before["treatment"]!["routes"]!.AsArray().Add(route);
            scenario.Before["treatment"]!["knownRouteIds"]!.AsArray().Add("inspect_explicit_method");
            var hidden = route.DeepClone().AsObject();
            hidden["routeId"] = "diagnostic_hidden_route"; hidden["visibility"] = "hidden";
            scenario.Before["treatment"]!["routes"]!.AsArray().Add(hidden);
            method["reveals"] = new JsonArray("route:diagnostic_hidden_route");
            scenario.Before["treatment"]!["diagnosisPaths"] = new JsonArray(method);
            // Explicit current physical recovery epoch, not the older procedure
            // fixture's read-only clock label or an absent recovery anchor.
            scenario.Before["recovery"]!["mode"] = "progressive";
            scenario.Before["recovery"]!["clockKind"] = "world_time.currentTimeInMinutes";
            scenario.Before["recovery"]!["cadence"] = 10;
            scenario.Before["recovery"]!["currentStepThreshold"] = 1000;
            scenario.Before["recovery"]!["blockers"] = new JsonArray();
            scenario.Before["recovery"]!["recoveryAnchor"] = new JsonObject
            {
                ["anchorKind"] = "creation", ["anchorMinute"] = scenario.AcceptedState["worldMinute"]!.DeepClone(),
                ["anchorTransitionId"] = scenario.Before["lastTransition"]!["transitionId"]!.DeepClone()
            };
            scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before), SeedCanonicalWoundEffects = true };
            var root = Path.Combine(Path.GetTempPath(), "boe-mortal-diagnosis-owner-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            files.EnsureDirectoryStructure();
            FileSystemManager.CanonicalWriteLease? lease = null;
            try
            {
                _ = AcceptedStateFixture.SeedCanonicalComponentData(files, scenario, "player", "player_current", WoundCarrierCatalog.PlayerPath);
                var stagedContext = ParseContext();
                var control = signedInput ? await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance)
                    .BuildControlForNextTurnAsync("Mortal World") : new ProgressionControl { CurrentRealm = "Mortal World" };
                if (signedInput)
                {
                    var wound = WoundMaterializationContract.Parse(scenario.Before.ToJsonString(), WoundCarrierCatalog.PlayerPath).Wound!;
                    var selected = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
                    {
                        MortalDiagnosis = new(wound.Owner, new("mortal_world", "npc", "field_medic_01", WoundCarrierCatalog.NpcPath),
                            "inspect_explicit_method", WoundDiagnosisEvidence.ComputePathFingerprint(wound, "inspect_explicit_method"),
                            MortalWoundDiagnosisInputCodec.ContextFingerprint(stagedContext))
                    };
                    var input = MortalWoundDiagnosisInputCodec.Create(new(scenario.AcceptedState["sessionId"]!.GetValue<string>(),
                        scenario.AcceptedState["requestId"]!.GetValue<string>(), scenario.AcceptedState["turn"]!.GetValue<int>(),
                        control), selected, stagedContext);
                    await files.WriteFileAtomicAsync(AcceptedMechanicsPlan.WoundCommandPath, input.ToJsonString());
                }
                await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
                {
                    SessionId = scenario.AcceptedState["sessionId"]!.GetValue<string>(),
                    RequestId = scenario.AcceptedState["requestId"]!.GetValue<string>(),
                    TurnNumber = scenario.AcceptedState["turn"]!.GetValue<int>(),
                    ProgressionControl = control,
                    CurrentRealm = "Mortal World", PlayerAction = "Осмотреть рану по отдельно подтверждённому способу.",
                    PreGeneratedDices1d20 = scenario.AcceptedState["acceptedDice"]!.AsArray().Select(value => value!.GetValue<int>()).ToArray()
                });
                lease = await files.AcquireCanonicalWriteLeaseAsync();
                var exported = signedInput
                    ? MortalWoundTreatmentAcceptedStateAuthority.ExportDiagnosisCurrent(files, lease, stagedContext, scenario.Before["woundId"]!.GetValue<string>())
                    : MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(files, lease, stagedContext, scenario.Before["woundId"]!.GetValue<string>());
                Assert.True(exported.IsValid, JsonSerializer.Serialize(exported.Issues));
                return new(root, files, lease, Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(exported.Authority), scenario.EventRef);
            }
            catch
            {
                if (lease is not null) await lease.DisposeAsync();
                Directory.Delete(root, true); throw;
            }
        }

        private static MortalWoundTreatmentAuthority.Context ParseContext()
        {
            var parsed = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
            {
                ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = "player", ["targetId"] = "player_current",
                ["providerKind"] = "npc", ["providerId"] = "field_medic_01", ["currentLocationId"] = "loc_field_clinic_001"
            }.ToJsonString(), "diagnosis.context");
            Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
            return Assert.IsType<MortalWoundTreatmentAuthority.Context>(parsed.Context);
        }

        internal async Task RebindAsync()
        {
            var woundId = Authority.CurrentWound.WoundId;
            await Lease.DisposeAsync(); Lease = await Files.AcquireCanonicalWriteLeaseAsync();
            var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportDiagnosisCurrent(Files, Lease, ParseContext(), woundId);
            Assert.True(exported.IsValid, JsonSerializer.Serialize(exported.Issues));
            Authority = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(exported.Authority);
        }

        internal Dictionary<string, byte[]> CaptureJson() => Directory.EnumerateFiles(Root, "*.json", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(Root, path).StartsWith(".boe_runtime", StringComparison.Ordinal))
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        internal void AssertJson(Dictionary<string, byte[]> before)
        {
            var after = CaptureJson(); Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
            foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
        }
        public async ValueTask DisposeAsync()
        {
            await Lease.DisposeAsync();
            Directory.Delete(Root, true);
        }
    }
}
