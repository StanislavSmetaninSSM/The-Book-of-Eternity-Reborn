using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundOpportunityAuthorityTests
{
    [Theory]
    [InlineData("formal", "combat_resolution")]
    [InlineData("qte", "qte_resolution")]
    [InlineData("combat", "combat_resolution")]
    [InlineData("trap", "trap_resolution")]
    [InlineData("check", "check_resolution")]
    [InlineData("hazard", "hazard_resolution")]
    [InlineData("narrative", "narrative_injury")]
    public void Compose_SealsEveryRegisteredMortalOrdinaryOpportunity(
        string adapterKind,
        string authorityKind)
    {
        var request = BuildRequest(
            adapterKind: adapterKind,
            authorityKind: authorityKind,
            maximumSeverityRank: 2);

        var result = WoundOpportunityAuthority.Compose(request);

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
        var opportunity = Assert.IsType<WoundOpportunityAuthority>(result.Opportunity);
        Assert.Equal("opportunity_test_001", opportunity.OpportunityId);
        Assert.Equal("wound-opportunity-test-001", opportunity.PublicRef);
        Assert.Equal("turn_42:wound_capable_event", opportunity.EventRef);
        Assert.Equal("mortal_world", opportunity.Owner.Realm);
        Assert.Equal("physical", opportunity.Domain);
        Assert.Equal("mortal_narrative_injury_v1", opportunity.ProfileKey);
        Assert.Null(opportunity.MinimumSeverityRank);
        Assert.Equal(2, opportunity.MaximumSeverityRank);
        Assert.Null(opportunity.GuaranteedTrigger);
        Assert.Equal("вы", opportunity.SafeContext.Target);
        Assert.Equal(
            new[] { "anatomical", "systemic", "other" },
            opportunity.SafeContext.AllowedLocationKinds);
        Assert.StartsWith("sha256:", opportunity.InputEvidenceFingerprint);
        Assert.StartsWith("sha256:", opportunity.AuthorityFingerprint);

        var repeated = WoundOpportunityAuthority.Compose(request);
        Assert.True(repeated.Success);
        Assert.Equal(
            opportunity.AuthorityFingerprint,
            repeated.Opportunity!.AuthorityFingerprint);
    }

    [Theory]
    [InlineData("formal")]
    [InlineData("narrative")]
    public void Compose_HarmlessAcceptedResultCannotCarryPositiveWoundMaximum(
        string adapterKind)
    {
        var request = BuildRequest(
            adapterKind: adapterKind,
            authorityKind: adapterKind == "formal"
                ? "qte_resolution"
                : "narrative_injury",
            outcomeKind: "harmless",
            maximumSeverityRank: 2);

        var result = WoundOpportunityAuthority.Compose(request);

        Assert.False(result.Success);
        Assert.Null(result.Opportunity);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_opportunity_harmless_conflict" &&
            issue.FilePath.EndsWith(
                ".eventEvidence.maximumSeverityRank",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Compose_MissingEventEvidenceFailsClosed()
    {
        var request = BuildRequest() with { EventEvidence = null! };

        var result = WoundOpportunityAuthority.Compose(request);

        Assert.False(result.Success);
        Assert.Null(result.Opportunity);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_opportunity_event_evidence_invalid");
    }

    [Fact]
    public void Compose_SealsTheCompleteAcceptedEventSetIntoOpportunityAuthority()
    {
        var request = BuildRequest();
        var selected = request.Binding.AcceptedEvents[0];
        var sibling = new WoundAcceptedEventAuthority(
            "turn_42:sibling_event",
            "resource_change",
            "resource_result_test_001",
            Fingerprint('a'));
        var acceptedEvents = new[] { selected, sibling };
        var binding = request.Binding with
        {
            AcceptedEvents = acceptedEvents,
            AcceptedEventsFingerprint =
                WoundAcceptedEventSetFingerprint.Compute(acceptedEvents)
        };

        var baseline = WoundOpportunityAuthority.Compose(request with
        {
            Binding = binding
        });
        var changedEvents = new[]
        {
            selected,
            sibling with { SemanticFingerprint = Fingerprint('b') }
        };
        var changed = WoundOpportunityAuthority.Compose(request with
        {
            Binding = binding with
            {
                AcceptedEvents = changedEvents,
                AcceptedEventsFingerprint =
                    WoundAcceptedEventSetFingerprint.Compute(changedEvents)
            }
        });

        Assert.True(baseline.Success);
        Assert.True(changed.Success);
        Assert.Equal(
            binding.AcceptedEventsFingerprint,
            baseline.Opportunity!.AcceptedEventsFingerprint);
        Assert.NotEqual(
            baseline.Opportunity.AuthorityFingerprint,
            changed.Opportunity!.AuthorityFingerprint);
    }

    [Theory]
    [InlineData("none", null, null)]
    [InlineData("materialize", 1, "wound_local_minor")]
    [InlineData("materialize", 2, "wound_local_equal_cap")]
    public void EvaluateDecision_AcceptsNoneLowerAndEqualMaximum(
        string decision,
        int? severityRank,
        string? woundRef)
    {
        var opportunity = ComposeValidOpportunity(maximumSeverityRank: 2);

        var result = WoundOpportunityDecisionAuthority.Evaluate(
            opportunity,
            new WoundOpportunityDecisionRequest(
                opportunity.PublicRef,
                decision,
                severityRank,
                woundRef),
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.True(result.Success);
        Assert.False(result.AlreadyConsumed);
        Assert.Empty(result.Issues);
        var accepted = Assert.IsType<WoundOpportunityDecisionAuthority>(result.Decision);
        Assert.Equal(decision, accepted.Decision);
        Assert.Equal(severityRank, accepted.SelectedSeverityRank);
        Assert.Equal(woundRef, accepted.LocalWoundRef);
        Assert.StartsWith("wound_operation_", accepted.OperationKey);
        Assert.StartsWith("sha256:", accepted.DecisionFingerprint);
    }

    [Fact]
    public void EvaluateDecision_RejectsSeverityAboveSealedMaximum()
    {
        var opportunity = ComposeValidOpportunity(maximumSeverityRank: 2);

        var result = WoundOpportunityDecisionAuthority.Evaluate(
            opportunity,
            new WoundOpportunityDecisionRequest(
                opportunity.PublicRef,
                "materialize",
                3,
                "wound_local_over_cap"),
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(result.Success);
        Assert.Null(result.Decision);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_severity_above_opportunity" &&
            issue.Expected == "I-II" &&
            issue.Actual == "III");
    }

    [Fact]
    public void EvaluateDecision_ConsumedDeclineReplaysExactlyButCannotBecomeInjury()
    {
        var opportunity = ComposeValidOpportunity(maximumSeverityRank: 2);
        var declineRequest = new WoundOpportunityDecisionRequest(
            opportunity.PublicRef,
            "none",
            null,
            null);
        var first = WoundOpportunityDecisionAuthority.Evaluate(
            opportunity,
            declineRequest,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        var accepted = Assert.IsType<WoundOpportunityDecisionAuthority>(first.Decision);
        var receipt = new WoundOpportunityDecisionReceipt(
            opportunity.OpportunityId,
            accepted.DecisionFingerprint,
            accepted.OperationKey);

        var exactReplay = WoundOpportunityDecisionAuthority.Evaluate(
            opportunity,
            declineRequest,
            new[] { receipt });
        var changedReplay = WoundOpportunityDecisionAuthority.Evaluate(
            opportunity,
            new WoundOpportunityDecisionRequest(
                opportunity.PublicRef,
                "materialize",
                1,
                "wound_local_after_decline"),
            new[] { receipt });

        Assert.True(exactReplay.Success);
        Assert.True(exactReplay.AlreadyConsumed);
        Assert.Equal(
            accepted.DecisionFingerprint,
            exactReplay.Decision!.DecisionFingerprint);
        Assert.Equal(accepted.OperationKey, exactReplay.Decision.OperationKey);
        Assert.False(changedReplay.Success);
        Assert.Null(changedReplay.Decision);
        Assert.Contains(changedReplay.Issues, issue =>
            issue.Code == "wound_opportunity_already_consumed");
    }

    [Fact]
    public void Compose_SealsExactWorseningTargetAndRejectsResealedMutation()
    {
        var before = ParseWound(WoundContractTestData.CreateActiveWound());
        var result = WoundOpportunityAuthority.Compose(BuildRequest(
            maximumSeverityRank: 3,
            worseningTarget: new WoundOpportunityWorseningTargetEvidence(
                before,
                "retrauma")));

        Assert.True(result.Success);
        var opportunity = Assert.IsType<WoundOpportunityAuthority>(
            result.Opportunity);
        var target = Assert.IsType<WoundOpportunityWorseningTargetAuthority>(
            opportunity.WorseningTarget);
        Assert.Equal(before.WoundId, target.Wound.WoundId);
        Assert.Equal(
            WoundIdentityState.ComputeSemanticFingerprint(before),
            target.ExpectedBeforeFingerprint);
        Assert.True(WoundOpportunityAuthority.HasCompleteShape(opportunity));

        var changed = before with
        {
            Display = before.Display with
            {
                Prognosis = "Подменённый прогноз после запечатывания."
            }
        };
        var tampered = opportunity with
        {
            WorseningTarget = new WoundOpportunityWorseningTargetAuthority(
                changed,
                target.CauseKind,
                target.ExpectedBeforeFingerprint)
        };

        Assert.False(WoundOpportunityAuthority.HasCompleteShape(tampered));
    }

    [Theory]
    [InlineData(1, "I")]
    [InlineData(2, "II")]
    public void EvaluateDecision_WorseningRejectsSameOrLowerSeverity(
        int severityRank,
        string severity)
    {
        var before = ParseWound(WoundContractTestData.CreateActiveWound());
        var opportunity = Assert.IsType<WoundOpportunityAuthority>(
            WoundOpportunityAuthority.Compose(BuildRequest(
                maximumSeverityRank: 3,
                worseningTarget: new WoundOpportunityWorseningTargetEvidence(
                    before,
                    "deterioration"))).Opportunity);

        var result = WoundOpportunityDecisionAuthority.Evaluate(
            opportunity,
            new WoundOpportunityDecisionRequest(
                opportunity.PublicRef,
                "materialize",
                severityRank,
                "wound_local_worsening"),
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(result.Success);
        Assert.Null(result.Decision);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_worsening_severity_not_higher" &&
            issue.Expected == "III-III" &&
            issue.Actual == severity);
    }

    private static WoundOpportunityAuthority ComposeValidOpportunity(
        int maximumSeverityRank)
    {
        var result = WoundOpportunityAuthority.Compose(BuildRequest(
            maximumSeverityRank: maximumSeverityRank));
        Assert.True(result.Success);
        return Assert.IsType<WoundOpportunityAuthority>(result.Opportunity);
    }

    private static WoundOpportunityBuildRequest BuildRequest(
        string adapterKind = "formal",
        string authorityKind = "combat_resolution",
        string outcomeKind = "harmful",
        int maximumSeverityRank = 2,
        WoundOpportunityWorseningTargetEvidence? worseningTarget = null)
    {
        var eventEvidence = new WoundOpportunityEventEvidence(
            adapterKind,
            authorityKind,
            "combat_result_test_001",
            outcomeKind,
            maximumSeverityRank,
            "Осколок стекла после обвала ранит цель.");
        var acceptedEvent = new WoundAcceptedEventAuthority(
            "turn_42:wound_capable_event",
            authorityKind,
            "combat_result_test_001",
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
        return new WoundOpportunityBuildRequest(
            binding,
            "opportunity_test_001",
            "wound-opportunity-test-001",
            acceptedEvent.EventRef,
            new WoundOwnerCoordinate(
                "mortal_world",
                "player",
                "player_current",
                "game_state/player/wounds.json"),
            "physical",
            "mortal_narrative_injury_v1",
            "combat_action",
            "combat_action_test_001",
            "active",
            eventEvidence,
            HardMaximumSeverityRank: 4,
            GuaranteedTrigger: null,
            new WoundOpportunitySafeContext(
                "вы",
                "осколок стекла после обвала",
                new[] { "anatomical", "systemic", "other" }),
            worseningTarget);
    }

    private static WoundMaterializationEnvelope ParseWound(JsonObject value)
    {
        var parsed = WoundMaterializationContract.Parse(
            value.ToJsonString(),
            "test.wound");
        Assert.True(parsed.IsValid);
        return Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
    }

    private static string Fingerprint(char value) =>
        "sha256:" + new string(value, 64);
}
