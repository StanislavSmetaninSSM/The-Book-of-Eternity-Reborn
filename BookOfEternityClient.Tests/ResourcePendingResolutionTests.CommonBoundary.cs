using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ResourcePendingResolutionTests
{
    [Fact]
    public void CommonBoundary_EmptyHasNoPendingReceiptOrCandidateObligation()
    {
        Assert.Empty(CheckCommonBoundary(null, new JsonObject()));
        Assert.Empty(CheckCommonBoundary(null,
            new JsonObject { ["effectResolutionReceipts"] = new JsonArray() }));
    }

    [Fact]
    public void CommonBoundary_ActualActivePendingRefusesUnboundedCalculation()
    {
        var created = CreatePending(Draft());
        Assert.True(created.IsValid, Format(created.Issues));
        Assert.Contains(CheckCommonBoundary(created.State, new JsonObject()),
            issue => issue.Code == "resource_pending_active_not_unbounded");
        Assert.Single(created.State!.Requests);
    }

    [Theory]
    [InlineData("same", true)]
    [InlineData("session", false)]
    [InlineData("request", false)]
    [InlineData("turn", false)]
    public void CommonBoundary_ActualTerminalRequiresReceiptsOnlyForExactFrame(
        string changedCoordinate, bool expectedRefusal)
    {
        var pending = CreatePending(Draft()).State!;
        var resolved = pending.Resolve(new JsonArray(Receipt("resource_delta", amount: 3)),
            Context(), ResourceDefinitionCatalog.CreateBuiltIn());
        Assert.True(resolved.IsValid, Format(resolved.Issues));
        var terminal = Assert.IsType<ResourcePendingResolutionState>(resolved.StateAfterImage);
        Assert.Empty(terminal.Requests);
        Assert.Single(terminal.TerminalReceipts);
        var before = terminal.ToCanonicalJson();
        var issues = AcceptedMechanicsPlanner.ValidateUnboundedCommonBoundary(
            changedCoordinate == "session" ? "session_other" : "session_test",
            changedCoordinate == "request" ? "request_other" : "request_turn_42",
            changedCoordinate == "turn" ? 43 : 42,
            terminal, new JsonObject(), Array.Empty<AcceptedEffectBoundedResourceResolution>());
        if (expectedRefusal)
            Assert.Contains(issues, issue => issue.Code == "resource_pending_terminal_replay_incomplete");
        else Assert.Empty(issues);
        Assert.Equal(before, terminal.ToCanonicalJson());
    }

    [Theory]
    [InlineData("null", "resource_pending_receipts_invalid")]
    [InlineData("{}", "resource_pending_receipts_invalid")]
    [InlineData("[{}]", "resource_pending_receipts_not_unbounded")]
    public void CommonBoundary_SubmittedReceiptsCannotBecomeUnboundedEmpty(
        string receiptJson, string expectedCode)
    {
        var commands = new JsonObject { ["effectResolutionReceipts"] = JsonNode.Parse(receiptJson) };
        var before = commands.ToJsonString();
        Assert.Contains(CheckCommonBoundary(null, commands), issue => issue.Code == expectedCode);
        Assert.Equal(before, commands.ToJsonString());
    }

    private static IReadOnlyList<ValidationIssue> CheckCommonBoundary(
        ResourcePendingResolutionState? state, JsonObject commands) =>
        AcceptedMechanicsPlanner.ValidateUnboundedCommonBoundary(
            "session_test", "request_turn_42", 42, state, commands,
            Array.Empty<AcceptedEffectBoundedResourceResolution>());
}
