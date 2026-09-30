using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundMaterializationLifecycleTests
{
    [Fact]
    public async Task AcceptedCommand_ForeignAcceptedEventFailsExactPreparationAuthority()
    {
        await using var context = await CreatePlayerContextAsync();
        var signed = await CreateSignedAuthorityAsync(context, maximumSeverityRank: 2);
        var authority = await ComposeForeignSignedEventAuthorityAsync(context, signed);
        var decision = Decision("none", proposal: null);
        decision["opportunityRef"] = authority.Opportunity.PublicRef;
        var response = Response(decision);
        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));

        var issues = await ValidateRejectedSignedCommandAsync(
            context,
            Assert.IsType<JsonObject>(composed.CommandRoot).ToJsonString());

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_wound_validation_opportunity_authority_mismatch");
        Assert.DoesNotContain(issues, issue => issue.Code == "mortal_wound_validation_occurrence_unresolved");
    }

    [Fact]
    public async Task AcceptedDecline_UnresolvedOwnerTargetFailsBeforeCommandConsumption()
    {
        await using var context = await CreatePlayerContextAsync();
        var owner = new WoundOwnerCoordinate(
            "mortal_world",
            "npc",
            "npc_not_in_accepted_turn",
            WoundCarrierCatalog.NpcPath);
        var authority = await CreateSignedAuthorityAsync(
            context,
            maximumSeverityRank: 2,
            acceptedOwner: owner);
        var decision = Decision("none", proposal: null);
        decision["opportunityRef"] = authority.Opportunity.PublicRef;
        var response = Response(decision);
        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));

        var issues = await ValidateRejectedSignedCommandAsync(
            context,
            Assert.IsType<JsonObject>(composed.CommandRoot).ToJsonString());

        Assert.Contains(issues, issue =>
            issue.Code == "wound_target_selector_unresolved");
        Assert.DoesNotContain(issues, issue => issue.Code == "mortal_wound_validation_occurrence_unresolved");
    }

    [Fact]
    public async Task AcceptedCommand_DuplicateNestedOpportunityFieldFailsStrictParsing()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var response = Response(Decision("none", proposal: null));
        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));
        var json = Assert.IsType<JsonObject>(composed.CommandRoot).ToJsonString();
        const string source =
            "\"sourceId\":\"combat_action_creation_player_001\"";
        const string duplicate =
            "\"sourceId\":\"combat_action_creation_player_001\"," +
            "\"sourceId\":\"combat_action_creation_player_001\"";
        var malformed = json.Replace(source, duplicate, StringComparison.Ordinal);
        Assert.NotEqual(json, malformed);

        var issues = await ValidateCommandAsync(context, malformed);

        Assert.Contains(issues, issue =>
            issue.Code == "wound_command_duplicate_field" &&
            issue.FilePath!.Contains("opportunity.sourceId", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("source_id", false)]
    [InlineData("profile", false)]
    [InlineData("owner", false)]
    [InlineData("event_kind", false)]
    [InlineData("guarantee_source", true)]
    public async Task AcceptedCommand_ChangedSealedCoordinateFailsRecomposition(
        string mutation,
        bool guaranteed)
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 2,
            guaranteedSeverityRank: guaranteed ? 2 : null);
        JsonObject? proposal = null;
        if (guaranteed)
        {
            proposal = CreatePhysicalProposal("II", includeMechanicalRoot: false);
            // This tamper test needs a legal complete proposal before changing the
            // sealed guarantee. Keep the unrelated global empty-treatment fixture.
            proposal["treatment"] = WoundContractTestData.CreateActiveWound()["treatment"]!.DeepClone();
        }
        var response = Response(Decision(guaranteed ? "materialize" : "none", proposal));
        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));
        var commandRoot = Assert.IsType<JsonObject>(composed.CommandRoot);
        var opportunity = commandRoot["commands"]![0]!["opportunity"]!.AsObject();
        switch (mutation)
        {
            case "source_id":
                opportunity["sourceId"] = "combat_action_changed_after_seal";
                break;
            case "profile":
                opportunity["profileKey"] = "mortal_changed_profile_v1";
                break;
            case "owner":
                opportunity["owner"]!["ownerId"] = "player_changed_after_seal";
                break;
            case "event_kind":
                opportunity["eventKind"] = "changed_event_kind";
                break;
            case "guarantee_source":
                opportunity["guaranteedTrigger"]!["sourceId"] =
                    "combat_action_changed_guarantee";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var issues = await ValidateCommandAsync(context, commandRoot.ToJsonString());

        Assert.Contains(issues, issue =>
            issue.Code == "wound_command_opportunity_invalid");
    }

    private static async Task<IReadOnlyList<ValidationIssue>> ValidateCommandAsync(
        ResourceMaterializationTestContext context,
        string json)
    {
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            json);
        return await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
    }

    [Fact]
    public async Task AcceptedCommand_SignedOccurrenceValidControlPreparesAcceptedPlan()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateSignedAuthorityAsync(context, maximumSeverityRank: 2);
        var decision = Decision("none", proposal: null);
        decision["opportunityRef"] = authority.Opportunity.PublicRef;
        var response = Response(decision);
        var composed = WoundResponseInputComposer.Compose(authority.Binding,
            new[] { authority.Opportunity }, response.WoundDecisions, response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.WoundCommandPath, composed.CommandRoot!.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(context.FileSystem, lease, out _), Describe(issues));
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _, out _), Describe(issues));
    }

    private static async Task<CreationAuthority> ComposeForeignSignedEventAuthorityAsync(
        ResourceMaterializationTestContext context, CreationAuthority signed)
    {
        var state = MortalWoundOccurrenceState.Parse(
            await context.FileSystem.ReadFileAsync(MortalWoundOccurrenceState.StatePath), MortalWoundOccurrenceState.StatePath);
        Assert.True(state.IsValid, Describe(state.Issues));
        var occurrence = Assert.Single(state.State!.Occurrences);
        var events = signed.Binding.AcceptedEvents.Select(value => value with
            { EventRef = "turn_42:foreign_accepted_effect" }).ToArray();
        var binding = signed.Binding with { AcceptedEvents = events,
            AcceptedEventsFingerprint = WoundAcceptedEventSetFingerprint.Compute(events) };
        var selected = Assert.Single(events);
        var composed = WoundOpportunityAuthority.Compose(new WoundOpportunityBuildRequest(
            binding, occurrence.OccurrenceId, occurrence.OpportunityRef, selected.EventRef,
            occurrence.Owner, occurrence.Domain, occurrence.ProfileKey, occurrence.Source.Kind,
            occurrence.Source.SourceId, occurrence.Source.State,
            new WoundOpportunityEventEvidence(occurrence.AdapterKind, selected.Kind, selected.AuthorityId,
                occurrence.Outcome.Kind, occurrence.Outcome.MaximumSeverityRank, occurrence.Outcome.ReadableCause),
            occurrence.HardMaximumSeverityRank, null, occurrence.SafeContext, null));
        Assert.True(composed.Success, Describe(composed.Issues));
        Assert.Equal(signed.Opportunity.OpportunityId, composed.Opportunity!.OpportunityId);
        Assert.Equal(signed.Opportunity.PublicRef, composed.Opportunity.PublicRef);
        Assert.Equal(signed.Opportunity.Owner, composed.Opportunity.Owner);
        // Deliberately do not reseed the signed snapshot with this foreign event.
        return new CreationAuthority(binding, composed.Opportunity);
    }

    private static async Task<IReadOnlyList<ValidationIssue>> ValidateRejectedSignedCommandAsync(
        ResourceMaterializationTestContext context, string json)
    {
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.WoundCommandPath, json);
        var paths = WoundMaterializationValidationTests.SnapshotWoundPaths
            .Append(AcceptedMechanicsPlan.WoundCommandPath)
            .Append(WoundMaterializationTestContext.NarrativeOutputPath).Distinct().ToArray();
        var before = new Dictionary<string, byte[]?>();
        foreach (var path in paths) before[path] = await ReadAcceptedTransitionBytesAsync(context, path);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(context.FileSystem, lease, out _));
        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
        Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(context.FileSystem, lease, out _));
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _, out _));
        foreach (var path in paths) Assert.Equal(before[path], await ReadAcceptedTransitionBytesAsync(context, path));
        return issues;
    }
}
