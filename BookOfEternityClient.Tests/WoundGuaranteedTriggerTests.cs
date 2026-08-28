using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundGuaranteedTriggerTests
{
    [Fact]
    public void Compose_PreMaterializedActiveTriggerSealsExactGuaranteedResult()
    {
        var request = BuildGuaranteedRequest(
            materializedAtTurn: 41,
            sourceState: "active",
            requiredSeverityRank: 2,
            eventMaximumSeverityRank: 3,
            hardMaximumSeverityRank: 4);

        var result = WoundOpportunityAuthority.Compose(request);

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
        var opportunity = Assert.IsType<WoundOpportunityAuthority>(result.Opportunity);
        Assert.Equal(2, opportunity.MinimumSeverityRank);
        Assert.Equal(3, opportunity.MaximumSeverityRank);
        var guarantee = Assert.IsType<WoundGuaranteedTriggerAuthority>(
            opportunity.GuaranteedTrigger);
        Assert.Equal("guaranteed_trigger_test_001", guarantee.TriggerId);
        Assert.Equal("skill_guaranteed_wound", guarantee.SourceId);
        Assert.Equal("active", guarantee.SourceState);
        Assert.Equal(2, guarantee.RequiredSeverityRank);
        Assert.Equal(41, guarantee.MaterializedAtTurn);
        Assert.StartsWith("sha256:", guarantee.AuthorityFingerprint);
    }

    [Fact]
    public void Compose_SameTurnTriggerIsNotPreMaterializedAuthority()
    {
        var result = WoundOpportunityAuthority.Compose(BuildGuaranteedRequest(
            materializedAtTurn: 42));

        Assert.False(result.Success);
        Assert.Null(result.Opportunity);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_guarantee_not_pre_materialized" &&
            issue.Actual == "42");
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("consumed")]
    [InlineData("suspended")]
    public void Compose_NonActiveGuaranteedSourceCannotPromiseResult(string sourceState)
    {
        var result = WoundOpportunityAuthority.Compose(BuildGuaranteedRequest(
            sourceState: sourceState));

        Assert.False(result.Success);
        Assert.Null(result.Opportunity);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_guarantee_source_inactive" &&
            issue.Actual == sourceState);
    }

    [Fact]
    public void Compose_GuaranteeAboveHardCapRejectsSourceContractWithoutCompromise()
    {
        var result = WoundOpportunityAuthority.Compose(BuildGuaranteedRequest(
            requiredSeverityRank: 3,
            eventMaximumSeverityRank: 4,
            hardMaximumSeverityRank: 2));

        Assert.False(result.Success);
        Assert.Null(result.Opportunity);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_guarantee_hard_cap_conflict" &&
            issue.Expected == "I-II" &&
            issue.Actual == "III");
    }

    [Fact]
    public void EvaluateDecision_GuaranteedTriggerRejectsOmittedResult()
    {
        var opportunity = ComposeGuaranteedOpportunity();

        var result = WoundOpportunityDecisionAuthority.Evaluate(
            opportunity,
            new WoundOpportunityDecisionRequest(
                opportunity.PublicRef,
                "none",
                null,
                null),
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(result.Success);
        Assert.Null(result.Decision);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_guaranteed_result_required");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void EvaluateDecision_GuaranteedTriggerRejectsDifferentSeverity(int severityRank)
    {
        var opportunity = ComposeGuaranteedOpportunity();

        var result = WoundOpportunityDecisionAuthority.Evaluate(
            opportunity,
            new WoundOpportunityDecisionRequest(
                opportunity.PublicRef,
                "materialize",
                severityRank,
                "wound_local_wrong_guarantee"),
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(result.Success);
        Assert.Null(result.Decision);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_guaranteed_severity_mismatch" &&
            issue.Expected == "II");
    }

    [Fact]
    public void ComposeAndEvaluate_ExactRetryPreservesEveryAuthorityCoordinate()
    {
        var request = BuildGuaranteedRequest();
        var firstOpportunity = Assert.IsType<WoundOpportunityAuthority>(
            WoundOpportunityAuthority.Compose(request).Opportunity);
        var retriedOpportunity = Assert.IsType<WoundOpportunityAuthority>(
            WoundOpportunityAuthority.Compose(request).Opportunity);
        var decisionRequest = new WoundOpportunityDecisionRequest(
            firstOpportunity.PublicRef,
            "materialize",
            2,
            "wound_local_guaranteed");
        var firstDecision = Assert.IsType<WoundOpportunityDecisionAuthority>(
            WoundOpportunityDecisionAuthority.Evaluate(
                firstOpportunity,
                decisionRequest,
                Array.Empty<WoundOpportunityDecisionReceipt>()).Decision);
        var receipt = new WoundOpportunityDecisionReceipt(
            firstOpportunity.OpportunityId,
            firstDecision.DecisionFingerprint,
            firstDecision.OperationKey);

        var retriedDecision = WoundOpportunityDecisionAuthority.Evaluate(
            retriedOpportunity,
            decisionRequest,
            new[] { receipt });

        Assert.Equal(
            firstOpportunity.AuthorityFingerprint,
            retriedOpportunity.AuthorityFingerprint);
        Assert.True(retriedDecision.Success);
        Assert.True(retriedDecision.AlreadyConsumed);
        Assert.Equal(
            firstDecision.DecisionFingerprint,
            retriedDecision.Decision!.DecisionFingerprint);
        Assert.Equal(firstDecision.OperationKey, retriedDecision.Decision.OperationKey);
    }

    [Fact]
    public void HasCompleteShape_RejectsGuaranteeSealedForAnotherOwner()
    {
        var primary = ComposeGuaranteedOpportunity();
        var foreignOwner = new WoundOwnerCoordinate(
            "mortal_world",
            "npc",
            "npc_foreign",
            "game_state/npcs/npc_wounds.json");
        var foreignRequest = BuildGuaranteedRequest();
        foreignRequest = foreignRequest with
        {
            Owner = foreignOwner,
            GuaranteedTrigger = foreignRequest.GuaranteedTrigger! with
            {
                Owner = foreignOwner
            }
        };
        var foreign = Assert.IsType<WoundOpportunityAuthority>(
            WoundOpportunityAuthority.Compose(foreignRequest).Opportunity);
        var changed = primary with
        {
            GuaranteedTrigger = foreign.GuaranteedTrigger,
            MinimumSeverityRank = foreign.MinimumSeverityRank
        };
        var resealed = changed with
        {
            AuthorityFingerprint =
                WoundOpportunityAuthority.RecomputeAuthorityFingerprint(changed)
        };

        Assert.False(WoundOpportunityAuthority.HasCompleteShape(resealed));
    }

    private static WoundOpportunityAuthority ComposeGuaranteedOpportunity()
    {
        var result = WoundOpportunityAuthority.Compose(BuildGuaranteedRequest());
        Assert.True(result.Success);
        return Assert.IsType<WoundOpportunityAuthority>(result.Opportunity);
    }

    private static WoundOpportunityBuildRequest BuildGuaranteedRequest(
        int materializedAtTurn = 41,
        string sourceState = "active",
        int requiredSeverityRank = 2,
        int eventMaximumSeverityRank = 3,
        int hardMaximumSeverityRank = 4)
    {
        var eventEvidence = new WoundOpportunityEventEvidence(
            "formal",
            "effect_trigger_resolution",
            "effect_trigger_test_001",
            "harmful",
            eventMaximumSeverityRank,
            "Запечатанный эффект источника вызывает рану.");
        var acceptedEvent = new WoundAcceptedEventAuthority(
            "turn_42:guaranteed_wound_trigger",
            "effect_trigger_resolution",
            "effect_trigger_test_001",
            WoundOpportunityEventEvidenceFingerprint.Compute(eventEvidence));
        var acceptedEvents = new[] { acceptedEvent };
        var binding = new WoundAcceptedTurnBinding(
            "session_test_001",
            "request_test_001",
            "snapshot_test_001",
            "mortal_world",
            42,
            acceptedEvents,
            WoundAcceptedEventSetFingerprint.Compute(acceptedEvents));
        var owner = new WoundOwnerCoordinate(
            "mortal_world",
            "player",
            "player_current",
            "game_state/player/wounds.json");
        return new WoundOpportunityBuildRequest(
            binding,
            "opportunity_guaranteed_test_001",
            "wound-opportunity-guaranteed-test-001",
            acceptedEvent.EventRef,
            owner,
            "physical",
            "mortal_guaranteed_trigger_v1",
            "skill",
            "skill_guaranteed_wound",
            sourceState,
            eventEvidence,
            hardMaximumSeverityRank,
            new WoundGuaranteedTriggerEvidence(
                "guaranteed_trigger_test_001",
                "skill",
                "skill_guaranteed_wound",
                sourceState,
                "mortal_world",
                "physical",
                owner,
                requiredSeverityRank,
                materializedAtTurn,
                "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
            new WoundOpportunitySafeContext(
                "вы",
                "запечатанный эффект источника",
                new[] { "anatomical", "systemic", "other" }));
    }
}
