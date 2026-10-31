using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public async Task DiagnosisProcedureEffectAgreementRequiresItsExactPrivatePreparedProgramAndCompleteInput()
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 0);
        var owner = fixture.Authority;
        var history = new WoundHistoryParseResult(owner.History, Array.Empty<ValidationIssue>());
        var result = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, owner.CurrentWound,
            "diagnosis_effect_agreement", "inspect_explicit_method", fixture.EventRef);
        Assert.True(result.IsValid, JsonSerializer.Serialize(result.Issues));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(result.Request);
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority);
        Assert.Null(check.PreparedCriticalReaction);
        await owner.RetainDiagnosisRequestAsync(request);
        var outcome = MortalWoundDiagnosisOutcome.Resolve(owner, history, request);
        Assert.Null(outcome.Resolution.CriticalReactionIntent);
        var working = MortalWoundDiagnosisWorkingProjection.Build(owner, outcome);
        var progression = await new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance)
            .PrepareDiagnosisOutcomeAsync(fixture.Lease, working);
        var preparedResult = WoundAcceptedTurnPlanner.PrepareMortalDiagnosisContinuation(owner, outcome, working, progression);
        Assert.True(preparedResult.Success, JsonSerializer.Serialize(preparedResult.Issues));
        var prepared = Assert.IsType<WoundPreparedAcceptedTurnPlan>(preparedResult.Plan);
        var continuation = Assert.IsType<WoundAcceptedTurnPlanner.MortalDiagnosisContinuation>(prepared.MortalDiagnosisAuthority);
        Assert.Null(continuation.Publication);
        var input = WoundAcceptedTurnPlanner.ComposeMortalDiagnosisEffectInput(prepared);
        var original = continuation.Input;
        var expectedEvents = EffectAcceptedTurnInputComposer.BuildAcceptedEventInput(owner.Binding.Turn, new JsonObject(),
            working.ResultingMinute, original.PreTurnEffectCarriers, original.PreTurnEffectCarriers, owner.Binding.Realm);
        Assert.True(JsonNode.DeepEquals(expectedEvents, input.EventInput));
        Assert.NotEmpty(input.EventInput["lifecycleEvents"]!.AsArray());
        Assert.Empty(input.RawCommands);
        Assert.NotNull(input.PreTurnCarriers);
        Assert.NotNull(input.PreTurnIdentityIndex);
        Assert.NotNull(input.PublicationCarrierBaselines);
        var frozenInput = WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input);
        var before = fixture.CaptureJson();
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(outcome.Resolution);
        Assert.True(finalization.IsValid, JsonSerializer.Serialize(finalization.Issues));
        var resource = Assert.IsType<MortalWoundTreatmentResourceFinalization>(finalization.Finalization);
        var heldBefore = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(fixture.Files, fixture.Lease, request, resource);
        Assert.True(heldBefore.IsValid, JsonSerializer.Serialize(heldBefore.Issues));

        Assert.True(WoundAcceptedTurnPlanner.MortalProcedureEffectInputAgrees(continuation, prepared, input));
        foreach (var fault in new[] { "omitted_lifecycle", "extra_lifecycle", "changed_lifecycle", "raw_commands", "session", "snapshot",
                     "realm", "turn", "time", "source", "target", "carriers", "identity", "publication_baseline", "malformed_event_input" })
        {
            var changed = WoundAcceptedTurnData.CloneEffectInput(input);
            switch (fault)
            {
                case "omitted_lifecycle": changed.EventInput.Remove("lifecycleEvents"); break;
                case "extra_lifecycle": changed.EventInput["lifecycleEvents"]!.AsArray().Add(changed.EventInput["lifecycleEvents"]![0]!.DeepClone()); break;
                case "changed_lifecycle": changed.EventInput["lifecycleEvents"]![0]!["phase"] = "scene_ended"; break;
                case "raw_commands": changed.RawCommands["effectChanges"] = new JsonArray(); break;
                case "session": changed = changed with { SessionId = input.SessionId + "_foreign" }; break;
                case "snapshot": changed = changed with { SnapshotToken = input.SnapshotToken + "_foreign" }; break;
                case "realm": changed = changed with { Realm = "chaos_sea" }; break;
                case "turn": changed.EventInput["turn"] = owner.Binding.Turn + 1; break;
                case "time": changed.EventInput["currentTime"] = working.ResultingMinute + 1; break;
                case "source": changed = changed with { SourceAuthority = null! }; break;
                case "target": changed = changed with { TargetAuthority = null! }; break;
                case "carriers": changed = changed with { PreTurnCarriers = null }; break;
                case "identity": changed = changed with { PreTurnIdentityIndex = null }; break;
                case "publication_baseline": changed = changed with { PublicationCarrierBaselines = null }; break;
                case "malformed_event_input": changed = changed with { EventInput = null! }; break;
            }
            Assert.False(WoundAcceptedTurnPlanner.MortalProcedureEffectInputAgrees(continuation, prepared, changed), fault);
        }

        // A separately created real private companion may have identical DATA;
        // its prepared scope cannot be substituted by copying fingerprints.
        var otherResult = WoundAcceptedTurnPlanner.PrepareMortalDiagnosisContinuation(owner, outcome, working, progression);
        Assert.True(otherResult.Success, JsonSerializer.Serialize(otherResult.Issues));
        var other = Assert.IsType<WoundPreparedAcceptedTurnPlan>(otherResult.Plan);
        var otherOwner = Assert.IsType<WoundAcceptedTurnPlanner.MortalDiagnosisContinuation>(other.MortalDiagnosisAuthority);
        Assert.NotSame(continuation, otherOwner);
        var otherInput = WoundAcceptedTurnPlanner.ComposeMortalDiagnosisEffectInput(other);
        Assert.True(WoundAcceptedTurnPlanner.MortalProcedureEffectInputAgrees(otherOwner, other, otherInput));
        Assert.False(WoundAcceptedTurnPlanner.MortalProcedureEffectInputAgrees(continuation, other, otherInput));
        Assert.False(WoundAcceptedTurnPlanner.MortalProcedureEffectInputAgrees(otherOwner, prepared, input));
        Assert.False(WoundAcceptedTurnPlanner.MortalProcedureEffectInputAgrees(new object(), prepared, input));
        Assert.Equal(frozenInput, WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input));
        Assert.True(JsonNode.DeepEquals(expectedEvents, input.EventInput));
        Assert.True(owner.HasLiveProcedureReservationAgreement(check));
        var heldAfter = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(fixture.Files, fixture.Lease, request, resource);
        Assert.True(heldAfter.IsValid, JsonSerializer.Serialize(heldAfter.Issues));
        Assert.Equal(heldBefore.State, heldAfter.State);
        Assert.Equal(request.RequestFingerprint, heldAfter.RequestFingerprint);
        fixture.AssertJson(before);
        // Component DATA agreement is not a publication/Fate proof. The actual
        // common callback and typed-Fate native publication remain required.
    }
}
