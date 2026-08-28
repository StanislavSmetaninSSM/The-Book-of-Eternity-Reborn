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
        var authority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 2,
            eventRef: "turn_42:foreign_accepted_effect");
        var response = Response(Decision("none", proposal: null));
        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));

        var issues = await ValidateCommandAsync(
            context,
            Assert.IsType<JsonObject>(composed.CommandRoot).ToJsonString());

        Assert.Contains(issues, issue =>
            issue.Code == "wound_materialization_event_authority_mismatch");
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
        var authority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 2,
            acceptedOwner: owner);
        var response = Response(Decision("none", proposal: null));
        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));

        var issues = await ValidateCommandAsync(
            context,
            Assert.IsType<JsonObject>(composed.CommandRoot).ToJsonString());

        Assert.Contains(issues, issue =>
            issue.Code == "wound_target_selector_unresolved");
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
        var response = guaranteed
            ? Response(Decision(
                "materialize",
                CreatePhysicalProposal("II", includeMechanicalRoot: false)))
            : Response(Decision("none", proposal: null));
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
}
