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
