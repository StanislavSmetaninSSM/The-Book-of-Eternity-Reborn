using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Local sealed reduction DATA only; no accepted turn or publication authority.</summary>
public sealed class SpiritualDiagnosisReductionTests
{
    [Theory]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public void ExactAdmittedSpiritualRequestReducesOnlyItsCompleteDeclaredKnowledge(string realm)
    {
        var prototype = SpiritualDiagnosisAdmissionTests.Context(realm);
        var node = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(
            Assert.Single(prototype.Catalog.Occurrences).Wound))!.AsObject();
        // This retained route is knowledge DATA, not an executable healing route.
        node["treatment"]!["routes"] = new JsonArray(new JsonObject
        {
            ["routeId"] = "revealed_route", ["displayName"] = "Известный способ помощи",
            ["visibility"] = "hidden", ["mode"] = "guaranteed", ["requirements"] = new JsonArray(),
            ["resourcePolicy"] = new JsonObject(), ["resolution"] = new JsonObject(),
            ["outcomes"] = new JsonArray(), ["interruption"] = null
        });
        node["treatment"]!["diagnosisPaths"]![0]!["reveals"] =
            new JsonArray("route:revealed_route", "complication:hidden_instability");
        var input = WoundPlayerProjectionTestData.InputInRealm(realm, node);
        input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"] =
            new JsonObject { ["spiritual_healing"] = 0 };
        var catalog = WoundCarrierCatalog.Build(input.Carriers);
        Assert.Empty(catalog.Issues);
        Assert.Empty(input.History.ValidateAgreement(input.Identities, catalog));
        var context = new WoundPlayerReadContext(input, catalog, prototype.Soul.DeepClone().AsObject(), null,
            WoundPlayerProjection.Build(input, catalog));
        Assert.True(context.Projection.IsAvailable);
        var before = Assert.Single(catalog.Occurrences).Wound;
        var admitted = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context)
            .Admit(before, "diagnose_tissue").Data);
        Assert.Equal(0, admitted.HealingTier);
        Assert.Empty(before.Treatment.KnownRouteIds);
        Assert.Equal("hidden", Assert.Single(before.Complications).Visibility);
        var path = Assert.Single(before.Treatment.DiagnosisPaths);
        Assert.Equal(new[] { "route:revealed_route", "complication:hidden_instability" }, path.Reveals);
        var turn = new SpiritualSelfTreatmentStageContext("session_diagnosis_reduction", "request_diagnosis_reduction", 43,
            new ProgressionControl { CurrentRealm = context.Soul["currentRealm"]!.GetValue<string>(), NextChaosSeaTurnOrdinal = 1 });
        var retained = SpiritualDiagnosisRequestCodec.Create(turn, admitted.Selection, admitted);
        Assert.Equal(admitted.Selection, SpiritualDiagnosisRequestCodec.ReadSelection(retained));
        Assert.True(SpiritualDiagnosisRequestCodec.IsExact(retained, turn, admitted.Selection, admitted));
        Assert.Equal(admitted.PathFingerprint, WoundDiagnosisEvidence.ComputePathFingerprint(before, path.DiagnosisPathId));

        const string transitionId = "transition_spiritual_diagnosis_reduction";
        var beforeCanonical = WoundMaterializationContract.SerializeCanonical(before);
        var proposed = JsonNode.Parse(beforeCanonical)!.AsObject();
        proposed["treatment"]!["knownRouteIds"] = new JsonArray("revealed_route");
        proposed["complications"]![0]!["visibility"] = "known_to_player";
        proposed["lastTransition"] = new JsonObject
        {
            ["transitionId"] = transitionId, ["ordinal"] = checked(before.LastTransition.Ordinal + 1),
            ["turn"] = turn.Turn, ["kind"] = "diagnose"
        };
        var parsed = WoundMaterializationContract.Parse(proposed.ToJsonString(), "diagnosis.proposed");
        Assert.True(parsed.IsValid, string.Join("; ", parsed.Issues.Select(issue => issue.Code + ": " + issue.Actual)));
        var after = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        Assert.Equal(new[] { "revealed_route" }, after.Treatment.KnownRouteIds);
        Assert.Equal("known_to_player", Assert.Single(after.Complications).Visibility);
        // Removing only the permitted knowledge/transition delta must recover the
        // complete original: rank, care, recovery, effects, prose and routes too.
        var normalized = after with
        {
            Complications = before.Complications,
            Treatment = after.Treatment with { KnownRouteIds = before.Treatment.KnownRouteIds },
            LastTransition = before.LastTransition
        };
        Assert.Equal(beforeCanonical, WoundMaterializationContract.SerializeCanonical(normalized));
        string Sources() => input.Carriers.AfterlifeProfiles!.ToJsonString() + context.Soul.ToJsonString()
            + WoundIdentityState.SerializeCanonical(input.Identities) + WoundHistoryState.SerializeCanonical(input.History);
        var sourceBytes = Sources();
        var requestBytes = retained.ToJsonString();
        var afterCanonical = WoundMaterializationContract.SerializeCanonical(after);
        var request = SpiritualDiagnosisTransitionPlanner.CreateKnowledgeRequest(turn, retained, admitted, after,
            transitionId, "operation_spiritual_diagnosis_reduction", "turn_43:spiritual_diagnosis");
        var evidence = Assert.IsType<WoundDiagnosisEvidence>(request.Evidence);
        Assert.Equal(admitted.RequirementFingerprint, evidence.RequirementAuthorityFingerprint);
        Assert.Equal(retained["requestFingerprint"]!.GetValue<string>(), evidence.CheckResultFingerprint);
        Assert.Equal(retained["diagnosis"]!["attemptId"]!.GetValue<string>(), evidence.AttemptId);
        Assert.Equal(beforeCanonical, WoundMaterializationContract.SerializeCanonical(request.Before!));
        Assert.Equal(afterCanonical, WoundMaterializationContract.SerializeCanonical(request.ProposedAfter!));
        var reduced = WoundTransitionReducer.Reduce(request);
        Assert.Equal(sourceBytes, Sources());
        Assert.Equal(requestBytes, retained.ToJsonString());
        Assert.Equal(beforeCanonical, WoundMaterializationContract.SerializeCanonical(before));
        Assert.Equal(afterCanonical, WoundMaterializationContract.SerializeCanonical(after));

        // Intended RED: the current reducer still rejects spiritual diagnosis at
        // its physical/Mortal-only semantic boundary, after valid sealed inputs.
        Assert.True(reduced.IsValid, string.Join("; ", reduced.Issues.Select(issue => issue.Code + ": " + issue.Actual)));
        Assert.Equal(3, reduced.Intents.Count);
        Assert.Single(reduced.Intents.OfType<WoundCarrierTransitionIntent>());
        Assert.Empty(reduced.Intents.OfType<WoundEffectTransitionIntent>());
        Assert.Equal(evidence.AttemptId, Assert.Single(reduced.Intents.OfType<WoundAttemptTerminalIntent>()).AttemptId);
        var result = Assert.IsType<WoundDiagnosisTransitionResult>(
            Assert.Single(reduced.Intents.OfType<WoundTransitionHistoryIntent>()).TransitionResult);
        Assert.Equal(path.Reveals, result.RevealedFacts);
        Assert.Equal(afterCanonical, WoundMaterializationContract.SerializeCanonical(reduced.ProposedAfter!));
    }
}
