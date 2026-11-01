using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
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
        var beforeForeignPending = fixture.CaptureJson();
        await fixture.Files.WriteFileAtomicAsync(fixture.Lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
            "{\"schemaVersion\":1,\"owner\":\"unowned pending DATA\"}");
        var foreignPending = fixture.CaptureJson();
        var foreignRefusal = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Authority.RetainDiagnosisRequestAsync(request));
        Assert.Contains("unowned pending before-image", foreignRefusal.Message);
        fixture.AssertJson(foreignPending);
        fixture.Files.DeleteFile(fixture.Lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        fixture.AssertJson(beforeForeignPending); // Restore only this pre-publication fault copy, not an accepted baseline.
        await fixture.Authority.RetainDiagnosisRequestAsync(request);
        var unchanged = fixture.CaptureJson();
        var outcome = MortalWoundDiagnosisOutcome.Resolve(fixture.Authority, history, request);
        var pendingPath = WoundAcceptedTurnSnapshotContract.PendingResolutionPath;
        var exactHeld = await fixture.Files.ReadFileBytesAsync(fixture.Lease, pendingPath);
        Assert.NotNull(exactHeld);
        // A whitespace-only rewrite remains valid request DATA, but was not
        // produced by the actual retained-request owner and cannot replace its
        // exact held after-image at a new lease admission.
        await fixture.Files.WriteFileAtomicBytesAsync(fixture.Lease, pendingPath, exactHeld.Concat(new byte[] { 32 }).ToArray());
        var refused = await fixture.RebindCandidateAsync();
        Assert.False(refused.IsValid);
        Assert.Contains(refused.Issues, value => value.Code == "mortal_diagnosis_retained_hold_changed");
        await fixture.Files.WriteFileAtomicBytesAsync(fixture.Lease, pendingPath, exactHeld);
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
    public Task DiagnosisWorkingTimeAndRecoveryUseOriginalSourcesWithoutFutureWrites(int duration) =>
        VerifyDiagnosisWorkingTimeAndRecoveryAsync(duration);

    [Fact]
    public Task DiagnosisElapsedTimePreservesOriginalCycleCarryUntilNextControlBuild() =>
        VerifyDiagnosisWorkingTimeAndRecoveryAsync(300);

    private static async Task VerifyDiagnosisWorkingTimeAndRecoveryAsync(int duration)
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
        Assert.Equal("08:00", working.ClockAfter["timeOfDay"]!.GetValue<string>()); // Preserved DATA, not a recalculated calendar.
        var displayedClock = working.ClockAfter;
        var clockElement = JsonSerializer.SerializeToElement(displayedClock);
        var expectedClock = "Игровое время: " + working.ResultingMinute + " мин.";
        Assert.Equal(expectedClock, StateManager.FormatWorldTime(clockElement));
        var consoleClock = new List<string>(); ExplorerMode.AppendWorldTimeLines(consoleClock, clockElement, "");
        Assert.Contains(consoleClock, line => line.Contains(expectedClock, StringComparison.Ordinal));
        var browserClock = Assert.Single(ExplorerMortalWorldCommandResultBuilder.BuildWorldTimeItems(displayedClock));
        Assert.Equal("Игровое время", browserClock.Key); Assert.Equal(working.ResultingMinute + " мин.", browserClock.Value);
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
        if (duration == 0) Assert.Equal(duration / cadence, after["pendingWorldCycles"]!.GetValue<int>());
        else
        {
            var originalSchedule = working.OriginalSchedule;
            Assert.Equal(originalSchedule["pendingWorldCycles"]!.GetValue<int>(), after["pendingWorldCycles"]!.GetValue<int>());
            Assert.Equal(originalSchedule["pendingFactionCycles"]!.GetValue<int>(), after["pendingFactionCycles"]!.GetValue<int>());
            foreach (var field in new[] { "lastWorldSimulationTimeInMinutes", "lastFactionSimulationTimeInMinutes" })
            {
                Assert.Equal(originalControl[field]!.GetValue<int>(), after[field]!.GetValue<int>());
                Assert.Equal(originalSchedule[field]!.GetValue<int>(), after[field]!.GetValue<int>());
            }
            // Ordinary settlement carries original obligations; the next control
            // build derives newly due work from T1 and these preserved markers.
            var nextWorldBacklog = (working.ResultingMinute - after["lastWorldSimulationTimeInMinutes"]!.GetValue<int>()) / cadence;
            Assert.Equal(duration / cadence, nextWorldBacklog);
            Assert.Equal(0, after["pendingWorldCycles"]!.GetValue<int>());
            Assert.Equal(0, after["pendingFactionCycles"]!.GetValue<int>());
            var expectedSchedule = originalSchedule.DeepClone().AsObject();
            expectedSchedule["currentWorldTimeInMinutes"] = working.ResultingMinute;
            expectedSchedule["lastUpdatedUtc"] = after["lastUpdatedUtc"]!.DeepClone();
            Assert.True(JsonNode.DeepEquals(expectedSchedule, after));
        }
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
                Assert.False(refused.IsValid);
                Assert.Contains(refused.Issues, issue => issue.Severity == IssueSeverity.Error &&
                    issue.Code == "wound_history_missing_field" && issue.FilePath ==
                    WoundHistoryState.HistoryPath + $".transitions[{rows.Count - 1}].transitionResult");
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
        foreach (var fault in new[] { "die_index", "die_value", "original_clock" })
        {
            var changed = root.DeepClone().AsObject();
            if (fault == "original_clock")
            {
                var clock = changed["clock"]!.AsObject();
                var originalClock = JsonNode.Parse(FileSystemManager.DecodeFileBytes(Convert.FromBase64String(clock["originalBase64"]!.GetValue<string>())))!.AsObject();
                originalClock["currentTimeInMinutes"] = owner.CurrentGameMinute + 1;
                clock["originalBase64"] = Convert.ToBase64String(FileSystemManager.EncodeUtf8WithPreamble(originalClock.ToJsonString()));
                changed["input"]!["control"]!["currentWorldTimeInMinutes"] = owner.CurrentGameMinute + 1;
                changed["input"]!["inputFingerprint"] = MortalWoundDiagnosisInputCodec.Fingerprint(changed["input"]!.AsObject());
                var schedule = JsonNode.Parse(FileSystemManager.DecodeFileBytes(Convert.FromBase64String(clock["scheduleBeforeBase64"]!.GetValue<string>())))!.AsObject();
                schedule["currentWorldTimeInMinutes"] = owner.CurrentGameMinute + 1;
                var scheduleBefore = FileSystemManager.EncodeUtf8WithPreamble(schedule.ToJsonString());
                clock["scheduleBeforeBase64"] = Convert.ToBase64String(scheduleBefore);
                var preparedAt = JsonNode.Parse(FileSystemManager.DecodeFileBytes(Convert.FromBase64String(clock["scheduleAfterBase64"]!.GetValue<string>())))!["lastUpdatedUtc"]!.GetValue<string>();
                var progression = ProgressionScheduleService.ReconstructDiagnosisOutcomeData(changed["input"]!["control"]!.AsObject(),
                    scheduleBefore, Convert.FromBase64String(clock["originalBase64"]!.GetValue<string>()), null,
                    owner.Binding.SessionId, owner.Binding.RequestId, owner.Binding.Turn, 0, preparedAt);
                clock["scheduleAfterBase64"] = Convert.ToBase64String(progression.ScheduleAfter.Bytes!);
                clock["progressionFingerprint"] = progression.MutationFingerprint;
            }
            else
            {
                var requestJson = changed["request"]!.AsObject();
                var procedure = ReadJsonObject(requestJson, "ModeAuthority");
                if (fault == "die_index")
                {
                    procedure[FindJsonPropertyName(procedure, "SourceIndices")]![0] = result.Source.AcceptedD20EventValues.Count;
                    procedure[FindJsonPropertyName(procedure, "SelectedSourceIndex")] = result.Source.AcceptedD20EventValues.Count;
                }
                else
                {
                    procedure[FindJsonPropertyName(procedure, "SourceRolls")]![0] = 19;
                    procedure[FindJsonPropertyName(procedure, "NaturalRoll")] = 19;
                }
                // Existing pure JSON fingerprint helpers: no reflection, live
                // source/claim factory or forged accepted receipt participates.
                ResealDetachedProcedureAuthorityAndRequest(requestJson);
            }
            var changedRequest = ParseFreshAuthorityRequest(changed["request"]!.AsObject());
            var terminal = MortalWoundDiagnosisOutcome.ReconstructResolution(changedRequest);
            var changedKnowledge = MortalWoundDiagnosisOutcome.ReconstructKnowledge(terminal);
            changed["knowledgeAfter"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(changedKnowledge));
            changed["terminal"] = MortalWoundDiagnosisPersistedResult.CreateTerminalProjection(terminal);
            var finalized = MortalWoundTreatmentResourceComposer.Finalize(terminal);
            Assert.True(finalized.IsValid, JsonSerializer.Serialize(finalized.Issues));
            changed["resourceFinalization"] = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(finalized.Finalization!);
            var members = working.Members.Select(member =>
            {
                var current = member.Original.WoundId == changedKnowledge.WoundId ? changedKnowledge : member.Original;
                var recovery = MortalWoundRecoveryPlanner.ReconstructAtMinute(result.Source, result.Binding, current,
                    working.ResultingMinute, member.ConsumedRecovery, member.ConsumedDeterioration);
                Assert.NotNull(recovery.Resolution);
                return new MortalWoundDiagnosisRecoveryMember(member.OriginalSourcePath, member.Original, current,
                    member.ConsumedRecovery, member.ConsumedDeterioration, recovery.Resolution!);
            }).ToArray();
            for (var index = 0; index < members.Length; index++)
                changed["cohort"]![index]!["resolution"] = MortalWoundRecoveryPersistedResult.ResolutionJson(members[index].Resolution);
            var clockData = changed["clock"]!.AsObject();
            var scheduleData = JsonNode.Parse(FileSystemManager.DecodeFileBytes(Convert.FromBase64String(clockData["scheduleBeforeBase64"]!.GetValue<string>())))!.AsObject();
            changed["workingFingerprint"] = MortalWoundDiagnosisWorkingProjection.ComputeComparisonFingerprint(changedRequest,
                terminal, result.Source.AcceptedStateFingerprint, working.OriginalMinute, working.ResultingMinute,
                changed["input"]!["control"]!.AsObject(), scheduleData, clockData["after"]!.AsObject(), members);
            changed["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(changed);
            var changedHistory = JsonNode.Parse(serializedHistory)!.AsObject();
            var changedRow = changedHistory["transitions"]!.AsArray()[^1]!.AsObject();
            changedRow["transitionId"] = changedKnowledge.LastTransition.TransitionId;
            changedRow["afterFingerprint"] = WoundIdentityState.ComputeSemanticFingerprint(changedKnowledge);
            changedRow["sourceFingerprint"] = changed["workingFingerprint"]!.DeepClone();
            changedRow["transitionResult"] = changed;
            var refused = WoundHistoryState.Parse(changedHistory.ToJsonString(), WoundHistoryState.HistoryPath);
            Assert.False(refused.IsValid);
            Assert.Contains(refused.Issues, value => value.Code == "mortal_wound_diagnosis_result_invalid" &&
                value.Actual?.Contains(fault == "original_clock" ? "original clock differs from its retained source minute" : "retained accepted d20 pool", StringComparison.Ordinal) == true);
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
        if (duration != 0)
        {
            var finalWound = stages[^1].After;
            Assert.Equal("active", finalWound.Lifecycle);
            Assert.True(complete.State.TryResolveRecoveryReplay(finalWound, working.ResultingMinute, out var receipt, out var replayIssues));
            Assert.Empty(replayIssues);
            var retained = Assert.IsType<MortalWoundDiagnosisPersistedResult>(Assert.Single(complete.State.Transitions,
                value => value.TransitionResult is MortalWoundDiagnosisPersistedResult).TransitionResult);
            var member = Assert.Single(retained.Recoveries, value => value.Original.WoundId == finalWound.WoundId);
            Assert.Equal(MortalWoundRecoveryPersistedResult.CreateReceipt(member.Resolution, finalWound.WoundId), receipt);
            Assert.False(complete.State.TryResolveRecoveryReplay(finalWound, working.ResultingMinute + 10, out _, out var laterIssues));
            Assert.Empty(laterIssues);
            var consumed = complete.State.GetRecoveryConsumption(finalWound);
            Assert.Equal(duration / 10, consumed.Recovery);
            var following = MortalWoundRecoveryPlanner.ReconstructAtMinute(retained.Source, retained.Binding, finalWound,
                working.ResultingMinute + 10, consumed.Recovery, consumed.Deterioration);
            Assert.NotNull(following.Resolution);
            Assert.Equal(1, following.Resolution!.ElapsedCadences);
            var changedWound = finalWound with { Recovery = finalWound.Recovery with { CurrentStepProgress = finalWound.Recovery.CurrentStepProgress + 1 } };
            Assert.False(complete.State.TryResolveRecoveryReplay(changedWound, working.ResultingMinute, out _, out var changedIssues));
            Assert.Contains(changedIssues, value => value.Code == "wound_history_recovery_replay_source_mismatch");
        }
        fixture.AssertJson(originals);
        await fixture.RebindAsync();
        // Frozen actual execution remains comparable historical DATA after its
        // resolving lease closes. It cannot execute again under that stale lease.
        var replayed = WoundAcceptedTurnPlanner.Finalize(prepared.Plan!, effects);
        Assert.True(replayed.Success, JsonSerializer.Serialize(replayed.Issues));
        Assert.Equal(final.Plan!.WoundFinalPlanFingerprint, replayed.Plan!.WoundFinalPlanFingerprint);
        var refusedExecution = WoundEffectBatchPlanner.Build(prepared.Plan!, effectInput, new EffectIdentityFactory());
        Assert.False(refusedExecution.Success);
        Assert.Contains(refusedExecution.Issues, value => value.Code == "mortal_diagnosis_execution_invalid");
        fixture.AssertJson(originals);
    }

    [Fact]
    public async Task DiagnosisCommonPlanKeepsHeldAndOuterOriginalPendingImagesDistinct()
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 300);
        var originalPending = await fixture.Files.ReadFileBytesAsync(fixture.Lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        var input = JsonNode.Parse(Assert.IsType<string>(await fixture.Files.ReadFileAsync(fixture.Lease, AcceptedMechanicsPlan.WoundCommandPath)))!.AsObject();
        var admission = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(fixture.Files, fixture.Lease,
            MortalWoundDiagnosisInputCodec.Read(input).Frame);
        var owner = admission.Owner;
        var request = admission.Request;
        var heldPending = await fixture.Files.ReadFileBytesAsync(fixture.Lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        Assert.NotNull(heldPending);
        Assert.False(originalPending is not null && originalPending.AsSpan().SequenceEqual(heldPending));
        var staged = await fixture.Files.ReadFileBytesAsync(fixture.Lease, AcceptedMechanicsPlan.WoundCommandPath);
        var heldTree = fixture.CaptureJson();
        var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
        var locationIssues = await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease);
        Assert.Empty(locationIssues);
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
        var refusal = await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(fixture.Lease);
        Assert.Contains(refusal, issue => issue.Code == "mortal_diagnosis_live_owner_unavailable");
        fixture.AssertJson(heldTree);
        var raw = await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(fixture.Lease,
            prepareMortalDiagnosis: async (lease, actual) =>
            {
                Assert.Equal(request.RequestFingerprint, actual.Request.RequestFingerprint);
                var prepared = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(fixture.Files, lease,
                    new GameResponse(), actual.Owner, actual.Request);
                return prepared.Issues;
            });
        Assert.DoesNotContain(raw, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.Files, fixture.Lease, out _, out var validated));
        Assert.True(validated.Success, JsonSerializer.Serialize(validated.Issues));
        var plan = validated.Plan!;
        Assert.NotNull(plan.TreatmentResourcePublicationAuthority);
        Assert.Equal("diagnosis", plan.TreatmentResourcePublicationAuthority!.RequestAuthority.Coordinates.Purpose);
        Assert.Equal(heldPending, plan.BeforeImages[WoundAcceptedTurnSnapshotContract.PendingResolutionPath].Bytes);
        Assert.Equal(staged, plan.BeforeImages[AcceptedMechanicsPlan.WoundCommandPath].Bytes);
        Assert.Contains(EffectAcceptedTurnInputComposer.WorldTimePath, plan.TouchedPaths);
        Assert.Contains(ProgressionScheduleService.SchedulePath, plan.TouchedPaths);
        Assert.Contains(WoundAcceptedTurnSnapshotContract.PendingResolutionPath, plan.ConsumedPaths);
        Assert.Contains(AcceptedMechanicsPlan.WoundCommandPath, plan.ConsumedPaths);
        Assert.Equal(owner.CurrentGameMinute, JsonNode.Parse(Assert.IsType<string>(await fixture.Files.ReadFileAsync(fixture.Lease,
            EffectAcceptedTurnInputComposer.WorldTimePath)))!["currentTimeInMinutes"]!.GetValue<long>());
        fixture.AssertJson(heldTree);
    }

    [Fact]
    public async Task PublishedDiagnosisCompensatesToHeldClaimsAndFreshOrdinaryOwnerReplaysItsRecovery()
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 300);
        var fs = fixture.Files;
        var originalInput = JsonNode.Parse(Assert.IsType<string>(await fs.ReadFileAsync(fixture.Lease, AcceptedMechanicsPlan.WoundCommandPath)))!.AsObject();
        var frame = MortalWoundDiagnosisInputCodec.Read(originalInput).Frame;
        var admitted = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(fs, fixture.Lease, frame);
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(admitted.Request.ModeAuthority);
        var heldImage = fixture.CaptureJson();
        var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease));
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
        var common = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(fs, fixture.Lease,
            new GameResponse(), admitted.Owner, admitted.Request);
        Assert.True(common.IsValid, JsonSerializer.Serialize(common.Issues));
        var plan = common.Plan!;
        await fixture.ReleaseLeaseAsync();
        var normalizer = new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(fs, normalizer, validator,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(published.TreatmentResourcePublicationTransaction);
        try
        {
            Assert.DoesNotContain(published.Issues, issue => issue.Severity == IssueSeverity.Error);
            Assert.NotNull(published.MechanicsPlan);
            var transient = WoundHistoryState.Parse(await fs.ReadFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
            Assert.True(transient.IsValid, JsonSerializer.Serialize(transient.Issues));
            var exact = Assert.IsType<MortalWoundDiagnosisPersistedResult>(Assert.Single(transient.State!.Transitions,
                row => row.TransitionResult is MortalWoundDiagnosisPersistedResult).TransitionResult);
            Assert.Equal(admitted.Request.RequestFingerprint, exact.Request.RequestFingerprint);
            var exactClock = exact.ToCanonicalJson()["clock"]!.AsObject();
            Assert.Equal(300, exactClock["resultingMinute"]!.GetValue<long>() - exactClock["originalMinute"]!.GetValue<long>());
            var compensation = await transaction.CompensateAsync(fs);
            Assert.True(compensation.IsValid, JsonSerializer.Serialize(compensation.Issues));
            Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Rearmed, compensation.Outcome);
            fixture.AssertJson(heldImage); // Expected held image was captured BEFORE publication.
        }
        finally { await ((IAsyncDisposable)transaction).DisposeAsync(); }
        await fixture.AcquireLeaseAsync();
        var retried = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(fs, fixture.Lease, frame, requireRetained: true);
        var exactCheck = Assert.IsType<MortalWoundProcedureCheckAuthority>(retried.Request.ModeAuthority);
        Assert.Equal(admitted.Request.RequestFingerprint, retried.Request.RequestFingerprint);
        Assert.Equal(check.SourceIndices, exactCheck.SourceIndices); Assert.Equal(check.SourceRolls, exactCheck.SourceRolls);
        var retryPlan = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(fs, fixture.Lease,
            new GameResponse(), retried.Owner, retried.Request);
        Assert.True(retryPlan.IsValid, JsonSerializer.Serialize(retryPlan.Issues));
        Assert.Equal(plan.PreparedPlanFingerprint, retryPlan.Plan!.PreparedPlanFingerprint);
        await fixture.ReleaseLeaseAsync();
        var accepted = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(fs, normalizer, validator,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var second = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(accepted.TreatmentResourcePublicationTransaction);
        try
        {
            Assert.DoesNotContain(accepted.Issues, issue => issue.Severity == IssueSeverity.Error);
            var advanced = await second.AdvancePublishedAgreementWithProgressionAsync(fs,
                new ProgressionScheduleService(fs, NullLogger<ProgressionScheduleService>.Instance), frame.Progression);
            Assert.True(advanced.IsValid, JsonSerializer.Serialize(advanced.Issues));
            var complete = await second.CompleteAsync(fs);
            Assert.True(complete.IsValid, JsonSerializer.Serialize(complete.Issues));
            Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Finalized, complete.Outcome);
        }
        finally { await ((IAsyncDisposable)second).DisposeAsync(); }
        var durable = WoundHistoryState.Parse(await fs.ReadFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        Assert.True(durable.IsValid, JsonSerializer.Serialize(durable.Issues));
        var result = Assert.IsType<MortalWoundDiagnosisPersistedResult>(Assert.Single(durable.State!.Transitions,
            row => row.TransitionResult is MortalWoundDiagnosisPersistedResult).TransitionResult);
        var member = Assert.Single(result.Recoveries);
        Assert.NotEmpty(member.Stages);
        Assert.Equal("active", member.Stages[^1].AfterWound.Lifecycle);
        var minute = JsonNode.Parse(Assert.IsType<string>(await fs.ReadFileAsync(EffectAcceptedTurnInputComposer.WorldTimePath)))!["currentTimeInMinutes"]!.GetValue<long>();
        Assert.Equal(result.ToCanonicalJson()["clock"]!["resultingMinute"]!.GetValue<long>(), minute);
        // A genuine next original is captured from the actual published world.
        // No canonical clock/wound/source is patched to invent a later journey.
        var control = await new ProgressionScheduleService(fs, NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Mortal World");
        await new LiveTurnPreparationService(fs).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = frame.SessionId, RequestId = "diagnosis_next_ordinary_request", TurnNumber = frame.Turn + 1,
            CurrentRealm = "Mortal World", ProgressionControl = control, PlayerAction = "Продолжить обычное действие.",
            PreGeneratedDices1d20 = new[] { 12 }
        });
        await fixture.AcquireLeaseAsync();
        var fresh = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(fs, fixture.Lease,
            MortalWoundDiagnosisInputCodec.Read(originalInput).Context, member.Original.WoundId);
        Assert.True(fresh.IsValid, JsonSerializer.Serialize(fresh.Issues));
        var ordinary = fresh.Authority!;
        Assert.Equal(minute, ordinary.CurrentGameMinute);
        var beforeRead = fixture.CaptureJson();
        var recovery = MortalWoundRecoveryPlanner.Plan(fs, fixture.Lease, ordinary.Binding, ordinary.CurrentWound.WoundId);
        Assert.Equal(MortalWoundRecoveryPlanningDisposition.ExactReplay, recovery.Disposition);
        Assert.Empty(recovery.Issues); Assert.Null(recovery.Resolution);
        Assert.Equal(member.Resolution.TickKey, recovery.ReplayReceipt!.TickKey);
        fixture.AssertJson(beforeRead);
    }

    [Fact]
    public async Task DiagnosisRetainsOldestActualFateShieldAndPublishesOnlyItsDeclaredReaction()
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(1, signedInput: true, duration: 0, seedFate: true);
        var owner = fixture.Authority;
        var history = new WoundHistoryParseResult(owner.History, Array.Empty<ValidationIssue>());
        var first = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, owner.CurrentWound,
            "diagnosis_actual_fate", "inspect_explicit_method", fixture.EventRef);
        Assert.True(first.IsValid, JsonSerializer.Serialize(first.Issues));
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(first.Request!.ModeAuthority);
        Assert.Equal("player", check.RollActorKind);
        Assert.Equal("player_current", check.RollActorId);
        Assert.Equal(1, check.NaturalRoll);
        var reaction = Assert.IsType<MortalWoundPreparedCriticalReaction>(check.PreparedCriticalReaction);
        Assert.Equal("effect_fate_shield_older", reaction.EffectId);
        var outcome = MortalWoundDiagnosisOutcome.Resolve(owner, history, first.Request);
        Assert.Equal("failed_attempt", outcome.Resolution.ResultCategory);
        var intent = Assert.IsType<MortalWoundCriticalReactionIntent>(outcome.Resolution.CriticalReactionIntent);
        Assert.Equal(reaction.PreparedReactionFingerprint, intent.PreparedReactionFingerprint);
        await owner.RetainDiagnosisRequestAsync(first.Request);
        await fixture.RebindAsync();
        owner = fixture.Authority;
        var recovered = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, owner.CurrentWound,
            "diagnosis_actual_fate", "inspect_explicit_method", fixture.EventRef);
        Assert.True(recovered.IsValid, JsonSerializer.Serialize(recovered.Issues));
        var exact = Assert.IsType<MortalWoundProcedureCheckAuthority>(recovered.Request!.ModeAuthority);
        Assert.Equal(check.SourceIndices, exact.SourceIndices); Assert.Equal(check.SourceRolls, exact.SourceRolls);
        Assert.NotNull(exact.PreparedCriticalReaction);
        Assert.Equal(reaction.EffectId, exact.PreparedCriticalReaction.EffectId);
        Assert.Equal(reaction.TriggerId, exact.PreparedCriticalReaction.TriggerId);
        Assert.Equal(reaction.AcceptedEffectFingerprint, exact.PreparedCriticalReaction.AcceptedEffectFingerprint);
        Assert.Equal(reaction.PreparedReactionFingerprint, exact.PreparedCriticalReaction.PreparedReactionFingerprint);
        Assert.Equal(intent.IntentFingerprint,
            MortalWoundDiagnosisOutcome.Resolve(owner, history, recovered.Request).Resolution.CriticalReactionIntent!.IntentFingerprint);
        var unchanged = fixture.CaptureJson();
        var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease));
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
        var common = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(fixture.Files, fixture.Lease,
            new GameResponse(), owner, recovered.Request);
        Assert.True(common.IsValid, JsonSerializer.Serialize(common.Issues));
        var stage = Assert.IsType<AcceptedMechanicsWoundStageBundle>(common.Plan!.WoundStageBundle);
        var prepared = stage.PreparedPlan;
        var continuation = Assert.IsType<WoundAcceptedTurnPlanner.MortalDiagnosisContinuation>(prepared.MortalDiagnosisAuthority);
        var effectInput = stage.EffectBatchPlan.EffectInput;
        var lifecycle = effectInput.EventInput["lifecycleEvents"]!.AsArray();
        Assert.Equal(2, lifecycle.Count); // One real base event followed by the sealed typed Fate event.
        Assert.NotEqual(WoundAcceptedTurnFingerprintWriter.CanonicalJson(lifecycle[0]),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(lifecycle[1]));
        var fingerprint = WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, effectInput);
        Assert.True(WoundAcceptedTurnPlanner.MortalProcedureEffectInputAgrees(continuation, prepared, effectInput));
        Assert.True(owner.HasLiveProcedureReservationAgreement(exact));
        var reversed = WoundAcceptedTurnData.CloneEffectInput(effectInput);
        reversed.EventInput["lifecycleEvents"] = new JsonArray(lifecycle.Reverse().Select(value => value!.DeepClone()).ToArray());
        Assert.False(WoundAcceptedTurnPlanner.MortalProcedureEffectInputAgrees(continuation, prepared, reversed));
        Assert.NotEqual(fingerprint, WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, reversed));
        Assert.Equal(fingerprint, WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, effectInput));
        Assert.True(owner.HasLiveProcedureReservationAgreement(exact));
        var identities = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(common.Plan!.EffectPlan!.IdentityIndexAfterImage), EffectIdentityState.StatePath);
        Assert.NotNull(identities.State);
        Assert.Empty(identities.Issues);
        Assert.Equal("expired", Assert.Single(identities.State!.Entries, row => row.EffectId == "effect_fate_shield_older").State);
        Assert.Equal("active", Assert.Single(identities.State.Entries, row => row.EffectId == "effect_fate_shield_newer").State);
        fixture.AssertJson(unchanged);
    }

    private static ResolverScenario CreateDiagnosisInitialScenario(int? natural, int duration, bool seedFate)
    {
        var scenario = CreateScenario("procedure_normal_uses_lowest_free_die", "procedure");
        if (seedFate) scenario = scenario with { SeedFateEffectId = "effect_fate_shield_older" };
        if (natural is not null) scenario.AcceptedState["acceptedDice"] = new JsonArray(natural.Value, 17, 1, 20);
        var method = WoundContractTestData.CreateMortalDiagnosisPath(true, duration);
        if (seedFate) method["requirements"]![1]!["actorRole"] = "target";
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
        return scenario;
    }

    /// <summary>Seeds the same complete initial diagnosis graph before the caller's sole resource bootstrap, without preparing an original or issuing live authority.</summary>
    internal static WoundMaterializationEnvelope SeedGuidedDiagnosisInitialWorld(FileSystemManager files,
        Action<FileSystemManager> beforeResourceBootstrap)
    {
        var scenario = CreateDiagnosisInitialScenario(null, 30, false);
        _ = AcceptedStateFixture.SeedCanonicalComponentData(files, scenario, "player", "player_current",
            WoundCarrierCatalog.PlayerPath, beforeResourceBootstrap);
        var parsed = WoundMaterializationContract.Parse(scenario.Before.ToJsonString(), WoundCarrierCatalog.PlayerPath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        return Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
    }

    /// <summary>
    /// Supplies complete initial physical treatment DATA before the caller's one
    /// resource bootstrap/original. Existing route costs and course windows remain
    /// authored values; no attempt, receipt or accepted authority is created.
    /// </summary>
    internal static WoundMaterializationEnvelope SeedGuidedPhysicalInitialWorld(FileSystemManager files,
        string mode, Action<FileSystemManager> beforeResourceBootstrap)
    {
        var scenario = mode switch
        {
            "procedure" => CreateScenario("procedure_normal_uses_lowest_free_die", mode),
            "course" => CreateScalarCoursePublicationScenario(),
            "guaranteed" => CreateScenario("guaranteed_current_capability_proof_stabilizes", mode),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        // These real journeys now finish the canonical terminal program. The course
        // retains all three independently confirmed milestones and ordinary waits.
        var outcomes = scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray();
        if (mode == "course")
            outcomes[2]!["result"] = new JsonArray(new JsonObject { ["kind"] = "heal", ["legacies"] = new JsonArray() });
        else
            outcomes[0]!["result"] = new JsonArray(
                new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
                new JsonObject { ["kind"] = "heal", ["legacies"] = new JsonArray() });
        const int initialMinute = 1260;
        scenario.AcceptedState["worldMinute"] = initialMinute;
        // Current recovery coordinates are initial fixture declarations. Keep the
        // existing authored long cadence, so ordinary accepted waits do not invent
        // treatment duration or a second mechanical course transition.
        scenario.Before["recovery"]!["clockKind"] = "world_time.currentTimeInMinutes";
        scenario.Before["recovery"]!["recoveryAnchor"] = new JsonObject
        {
            ["anchorKind"] = "creation", ["anchorMinute"] = initialMinute,
            ["anchorTransitionId"] = scenario.Before["lastTransition"]!["transitionId"]!.DeepClone()
        };
        scenario = scenario with { WorldMinute = initialMinute,
            History = CreateCurrentWoundHistory(scenario.Before), SeedCanonicalWoundEffects = true };
        _ = AcceptedStateFixture.SeedCanonicalComponentData(files, scenario, "player", "player_current",
            WoundCarrierCatalog.PlayerPath, beforeResourceBootstrap);
        var parsed = WoundMaterializationContract.Parse(scenario.Before.ToJsonString(), "physicalJourney.initial");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        return parsed.Wound!;
    }

    internal sealed class DiagnosisOwnershipFixture : IAsyncDisposable
    {
        private DiagnosisOwnershipFixture(string root, FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalWoundTreatmentAcceptedStateAuthority authority, string eventRef)
        { Root = root; Files = files; Lease = lease; Authority = authority; EventRef = eventRef; }
        private string Root { get; }
        internal FileSystemManager Files { get; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; private set; }
        internal MortalWoundTreatmentAcceptedStateAuthority Authority { get; private set; }
        internal string EventRef { get; }

        internal static async Task<DiagnosisOwnershipFixture> CreateAsync(int? natural = null, bool signedInput = false, int duration = 30, bool seedFate = false,
            Func<FileSystemManager, TurnRequest, Task>? originalProducer = null, bool twoMembers = false, bool exportDiagnosis = true,
            FileSystemManagerHooks? hooks = null, string? woundLabel = null, bool resourceCost = false, string? physicalMode = null,
            Action<JsonObject>? preparePhysicalWound = null)
        {
            // Initial canonical scenario DATA only. The real live preparation and
            // exporter below issue ownership; these JSON values are not receipts.
            var scenario = CreateDiagnosisInitialScenario(natural, duration, seedFate);
            if (physicalMode is not null)
            {
                if (!signedInput || resourceCost && physicalMode != "guaranteed" || seedFate && physicalMode != "procedure")
                    throw new ArgumentException("Physical fixture requires its exact supported signed scenario.");
                scenario = physicalMode switch
                {
                    "procedure" => CreateScenario(seedFate ? "procedure_player_natural_one_reserves_oldest_fate_shield" :
                        "procedure_normal_uses_lowest_free_die", physicalMode),
                    "course" => CreateScalarCoursePublicationScenario(),
                    "guaranteed" => resourceCost ? CreateGuaranteedResourcePublicationScenario(new[] { 1, 1 }, new[] { 0, 1 }) :
                        CreateScenario("guaranteed_current_capability_proof_stabilizes", physicalMode),
                    _ => throw new ArgumentOutOfRangeException(nameof(physicalMode))
                };
                preparePhysicalWound?.Invoke(scenario.Before);
                if (natural is not null) scenario.AcceptedState["acceptedDice"] = new JsonArray(natural.Value, 17, 1, 20);
                scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before), SeedCanonicalWoundEffects = true };
            }
            if (resourceCost && physicalMode is null)
            {
                scenario.Before["treatment"]!["diagnosisPaths"]![0]!["requirements"]![0] = new JsonObject
                {
                    ["kind"] = "resource_quantity", ["resourceRef"] = "energy", ["quantity"] = 1, ["ownerRole"] = "target"
                };
                scenario.Before["treatment"]!["diagnosisPaths"]![0]!["requirements"]!.AsArray().Add(new JsonObject
                {
                    ["kind"] = "resource_quantity", ["resourceRef"] = "energy", ["quantity"] = 1, ["ownerRole"] = "target"
                });
                scenario.Before["treatment"]!["diagnosisPaths"]![0]!["check"]!["resourcePolicy"]!["mutations"]!.AsArray().Add(new JsonObject
                {
                    ["kind"] = "consume_requirement", ["scope"] = "common", ["milestoneOrdinal"] = null, ["requirementIndex"] = 2
                });
                scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before) };
            }
            if (woundLabel is not null)
            {
                scenario.Before["display"]!["name"] = woundLabel;
                scenario = scenario with { History = CreateCurrentWoundHistory(scenario.Before) };
            }
            var root = Path.Combine(Path.GetTempPath(), "boe-mortal-diagnosis-owner-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, hooks);
            files.EnsureDirectoryStructure();
            FileSystemManager.CanonicalWriteLease? lease = null;
            try
            {
                _ = AcceptedStateFixture.SeedCanonicalComponentData(files, scenario, "player", "player_current", WoundCarrierCatalog.PlayerPath);
                if (twoMembers) await SeedDiagnosisPrivateMemberAsync(files, scenario.Before);
                var clock = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(EffectAcceptedTurnInputComposer.WorldTimePath)))!.AsObject();
                clock["timeOfDay"] = "08:00";
                await files.WriteFileAtomicAsync(EffectAcceptedTurnInputComposer.WorldTimePath, clock.ToJsonString());
                var stagedContext = ParseContext();
                var control = signedInput ? await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance)
                    .BuildControlForNextTurnAsync("Mortal World") : new ProgressionControl { CurrentRealm = "Mortal World" };
                JsonObject? physicalGmContext = null;
                if (signedInput)
                {
                    var wound = WoundMaterializationContract.Parse(scenario.Before.ToJsonString(), WoundCarrierCatalog.PlayerPath).Wound!;
                    var selected = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound));
                    var provider = new WoundOwnerCoordinate("mortal_world", "npc", "field_medic_01", WoundCarrierCatalog.NpcPath);
                    selected = physicalMode is null ? selected with
                    { MortalDiagnosis = new(wound.Owner, provider, "inspect_explicit_method",
                        WoundDiagnosisEvidence.ComputePathFingerprint(wound, "inspect_explicit_method"), MortalWoundDiagnosisInputCodec.ContextFingerprint(stagedContext)) }
                        : selected with { MortalTreatment = new(wound.Owner, provider, scenario.RouteId, physicalMode,
                            physicalMode == "course" ? 1 : null, MortalWoundTreatmentRouteFingerprint.Compute(wound, scenario.RouteId),
                            MortalWoundGuidedTreatmentInputCodec.ContextFingerprint(stagedContext)) };
                    var frame = new SpiritualSelfTreatmentStageContext(scenario.AcceptedState["sessionId"]!.GetValue<string>(),
                        scenario.AcceptedState["requestId"]!.GetValue<string>(), scenario.AcceptedState["turn"]!.GetValue<int>(),
                        control);
                    var input = physicalMode is null ? MortalWoundDiagnosisInputCodec.Create(frame, selected, stagedContext)
                        : MortalWoundGuidedTreatmentInputCodec.Create(frame, selected, stagedContext);
                    if (physicalMode is not null) physicalGmContext = MortalWoundGuidedTreatmentInputCodec.CreateGmContext(input);
                    await files.WriteFileAtomicAsync(AcceptedMechanicsPlan.WoundCommandPath, input.ToJsonString());
                }
                var dices = scenario.AcceptedState["acceptedDice"]!.AsArray().Select(value => value!.GetValue<int>()).ToArray();
                if (originalProducer is not null)
                    await originalProducer(files, new TurnRequest
                    {
                        SessionId = scenario.AcceptedState["sessionId"]!.GetValue<string>(),
                        RequestId = scenario.AcceptedState["requestId"]!.GetValue<string>(),
                        TurnNumber = scenario.AcceptedState["turn"]!.GetValue<int>(), ProgressionControl = control,
                        PreGeneratedDices1d20 = dices, PlayerAction = "Осмотреть рану по явно заданному способу.",
                        MortalTreatment = physicalGmContext?.DeepClone().AsObject()
                    });
                else await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
                {
                    SessionId = scenario.AcceptedState["sessionId"]!.GetValue<string>(),
                    RequestId = scenario.AcceptedState["requestId"]!.GetValue<string>(),
                    TurnNumber = scenario.AcceptedState["turn"]!.GetValue<int>(),
                    ProgressionControl = control,
                    CurrentRealm = "Mortal World", PlayerAction = "Осмотреть рану по отдельно подтверждённому способу.",
                    PreGeneratedDices1d20 = dices, MortalTreatment = physicalGmContext
                });
                lease = await files.AcquireCanonicalWriteLeaseAsync();
                var exported = physicalMode is not null
                    ? MortalWoundTreatmentAcceptedStateAuthority.ExportGuidedTreatmentCurrent(files, lease, stagedContext, scenario.Before["woundId"]!.GetValue<string>())
                    : signedInput && exportDiagnosis
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

        internal Task ReleaseLeaseAsync() => Lease.DisposeAsync().AsTask();
        internal async Task AcquireLeaseAsync() => Lease = await Files.AcquireCanonicalWriteLeaseAsync();

        internal async Task RebindAsync()
        {
            var exported = await RebindCandidateAsync();
            Assert.True(exported.IsValid, JsonSerializer.Serialize(exported.Issues));
            Authority = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(exported.Authority);
        }

        internal async Task<MortalWoundTreatmentAcceptedStateAuthorityResult> RebindCandidateAsync()
        {
            var woundId = Authority.CurrentWound.WoundId;
            await Lease.DisposeAsync(); Lease = await Files.AcquireCanonicalWriteLeaseAsync();
            return Authority.HasOriginalGuidedTreatmentInput
                ? MortalWoundTreatmentAcceptedStateAuthority.ExportGuidedTreatmentCurrent(Files, Lease, ParseContext(), woundId)
                : MortalWoundTreatmentAcceptedStateAuthority.ExportDiagnosisCurrent(Files, Lease, ParseContext(), woundId);
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
