using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundMaterializationLifecycleTests
{
    [Theory]
    [InlineData("proposal_wound_id", "wound_response_unknown_field")]
    [InlineData("proposal_transition_id", "wound_response_unknown_field")]
    [InlineData("complication_id", "wound_response_unknown_field")]
    [InlineData("definition_effect_id", "wound_response_unknown_field")]
    [InlineData("root_effect_id", "wound_response_unknown_field")]
    [InlineData("slot_effect_id", "wound_response_unknown_field")]
    [InlineData("wound_link", "wound_response_client_authority_forbidden")]
    public async Task ResponseProposal_RejectsEveryGmAuthoredPermanentIdentitySurface(
        string mutation,
        string expectedCode)
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var proposal = CreatePhysicalProposal("I", includeMechanicalRoot: true);
        ApplyClientAuthorityMutation(proposal, mutation);
        var response = Response(Decision("materialize", proposal));

        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(composed.Success);
        Assert.Null(composed.CommandRoot);
        Assert.Empty(composed.Transitions);
        Assert.Empty(composed.Notifications);
        Assert.Contains(composed.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public async Task ResponseDecision_RejectsDuplicateJsonPropertyBeforeAuthorityEvaluation()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        using var document = JsonDocument.Parse(
            $"{{\"opportunityRef\":\"{OpportunityRef}\",\"decision\":\"none\",\"decision\":\"none\"}}");

        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            new[] { document.RootElement.Clone() },
            "Вы отступаете от опасного края.",
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(composed.Success);
        Assert.Contains(composed.Issues, issue =>
            issue.Code == "wound_response_duplicate_field" &&
            issue.FilePath == "woundDecisions[0].decision");
    }

    [Fact]
    public async Task ResponseProposal_UnknownFieldBesideMalformedKnownSiblingFailsRepairClosed()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var proposal = CreatePhysicalProposal("I", includeMechanicalRoot: true);
        proposal["unexpectedNote"] = "remove me";
        proposal["display"] = 17;
        var response = Response(Decision("materialize", proposal));

        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(composed.Success);
        Assert.Contains(composed.Issues, issue =>
            issue.Code == "wound_response_unknown_field" &&
            issue.FilePath == "woundDecisions[0].proposal.unexpectedNote");
        Assert.All(composed.Issues, issue => Assert.Null(issue.WoundRepairContext));
        Assert.Empty(WoundRepairPacketBuilder.Build(composed.Issues));
        Assert.True(WoundRepairPacketBuilder.RequiresFailClosedRollback(
            composed.Issues));
    }

    [Fact]
    public async Task ResponseProposal_TwoUnknownFieldsProduceOneAtomicRepairPacket()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var proposal = CreatePhysicalProposal("I", includeMechanicalRoot: true);
        proposal["unexpectedNote"] = "remove me";
        proposal["unusedDecoration"] = "remove me too";
        var response = Response(Decision("materialize", proposal));

        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(composed.Success);
        var repairIssues = composed.Issues.Where(issue =>
                issue.Code == "wound_response_unknown_field")
            .ToArray();
        Assert.Equal(2, repairIssues.Length);
        Assert.All(repairIssues, issue => Assert.NotNull(issue.WoundRepairContext));
        Assert.Single(repairIssues.Select(issue =>
            issue.WoundRepairContext!.CandidateRef).Distinct(StringComparer.Ordinal));

        var packet = Assert.Single(WoundRepairPacketBuilder.Build(composed.Issues));
        Assert.Equal(
            new[]
            {
                "proposal.unexpectedNote",
                "proposal.unusedDecoration"
            },
            packet.Issues.Select(issue => issue.Path).Order(StringComparer.Ordinal));
        Assert.False(packet.PreservedProposal.ContainsKey("unexpectedNote"));
        Assert.False(packet.PreservedProposal.ContainsKey("unusedDecoration"));
        Assert.False(WoundRepairPacketBuilder.RequiresFailClosedRollback(
            composed.Issues));
    }

    [Fact]
    public async Task ResponseProposal_MalformedDefinitionScalarFailsClosedWithoutThrowing()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var proposal = CreatePhysicalProposal("I", includeMechanicalRoot: true);
        proposal["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!
            ["profile"] = 17;
        var response = Response(Decision("materialize", proposal));
        WoundResponseInputCompositionResult? composed = null;

        var exception = Record.Exception(() => composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>()));

        Assert.Null(exception);
        Assert.NotNull(composed);
        Assert.False(composed.Success);
        Assert.Null(composed.CommandRoot);
        Assert.NotEmpty(composed.Issues);
    }

    [Fact]
    public void GameResponse_SerializesStrictWoundDecisionFieldByExactApiName()
    {
        var response = Response(Decision("none", proposal: null));

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response));

        Assert.True(document.RootElement.TryGetProperty("woundDecisions", out var decisions));
        Assert.Equal(JsonValueKind.Array, decisions.ValueKind);
        Assert.Single(decisions.EnumerateArray());
    }

    private static void ApplyClientAuthorityMutation(JsonObject proposal, string mutation)
    {
        var definition = proposal["consequenceDefinitions"]![0]!.AsObject();
        var root = definition["root"]!.AsObject();
        var slot = root["slots"]![0]!.AsObject();
        switch (mutation)
        {
            case "proposal_wound_id":
                proposal["woundId"] = "wound_gm_forbidden";
                break;
            case "proposal_transition_id":
                proposal["transitionId"] = "wound_transition_gm_forbidden";
                break;
            case "complication_id":
                proposal["complications"]!.AsArray().Add(new JsonObject
                {
                    ["complicationRef"] = "complication_local_forbidden_case",
                    ["complicationId"] = "wound_complication_gm_forbidden",
                    ["kind"] = "infection",
                    ["state"] = "active",
                    ["displayName"] = "Воспаление",
                    ["treatmentDifficultyModifier"] = 1,
                    ["visibility"] = "known_to_player"
                });
                break;
            case "definition_effect_id":
                definition["effectId"] = "effect_gm_forbidden";
                break;
            case "root_effect_id":
                root["effectId"] = "effect_gm_forbidden";
                break;
            case "slot_effect_id":
                slot["effectId"] = "effect_gm_forbidden";
                break;
            case "wound_link":
                definition["definition"]!["links"]!.AsArray().Add(new JsonObject
                {
                    ["kind"] = "wound",
                    ["targetId"] = "wound_gm_forbidden",
                    ["role"] = "source"
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
    }
}
