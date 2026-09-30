using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Reduces a signed explicit decline into detached receipt history and real common-plan work.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC3Decline_AppendsReceiptWithoutPublishingWound()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var opened = await adapter.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using var offer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var completed = await offer.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = offer.Offer!.OpportunityRef,
                decision = "none"
            }), null);
        Assert.Equal("completed_unpublished", completed.Disposition);
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        var physicalBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundOpportunityReceiptState.StatePath);

        var reduced = await terminal.ReduceCompletedDeclinesAsync(lease);

        Assert.True(reduced.Success, string.Join(Environment.NewLine,
            reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        var receipt = Assert.IsType<JsonObject>(reduced.ReceiptAfterImage);
        Assert.Single(receipt["instances"]!.AsArray());
        var source = Assert.Single(receipt["sources"]!.AsArray())!.AsObject();
        var decision = Assert.Single(receipt["decisions"]!.AsArray())!.AsObject();
        var pending = JsonNode.Parse(await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath))!["pending"]!;
        Assert.Equal("none", decision["decision"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(source["witness"], decision["sourceWitness"]));
        Assert.True(JsonNode.DeepEquals(source["witness"], pending["sources"]![0]));
        Assert.Equal(pending["stagedDecisions"]![0]!["opportunityRef"]!.GetValue<string>(),
            decision["opportunityRef"]!.GetValue<string>());
        Assert.Null(decision["woundId"]);
        Assert.Null(decision["transitionId"]);
        Assert.Equal(physicalBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundOpportunityReceiptState.StatePath));
        var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduced.Reduction);
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                ordinary, ordinary.Resources));
        Assert.Empty(planned.Issues);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planned.Plan);
        Assert.Contains(SpiritualWoundOpportunityReceiptState.StatePath, plan.TouchedPaths);
        Assert.True(JsonNode.DeepEquals(receipt,
            plan.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath]));
        Assert.Null(plan.WoundStageBundle);
        Assert.DoesNotContain(WoundIdentityState.StatePath, plan.TouchedPaths);
        Assert.DoesNotContain(WoundHistoryState.HistoryPath, plan.TouchedPaths);
    }

    /// <summary>
    /// Preserves both accepted sides of one exchange with distinct lifetime ordinals and shared causal binding.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC3Decline_AppendsBothSourceWitnessesInOrder()
    {
        await using var context = await CreateTwoSourceC2ContextAsync();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var opened = await adapter.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using var first = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var advanced = await first.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = first.Offer!.OpportunityRef,
                decision = "none"
            }), null);
        Assert.Equal("offer", advanced.Disposition);
        using var second = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(advanced.Session);
        var completed = await second.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = second.Offer!.OpportunityRef,
                decision = "none"
            }), null);
        Assert.Equal("completed_unpublished", completed.Disposition);
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);

        var reduced = await terminal.ReduceCompletedDeclinesAsync(lease);

        Assert.True(reduced.Success, string.Join(Environment.NewLine,
            reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        var receipt = reduced.ReceiptAfterImage!;
        var sources = receipt["sources"]!.AsArray();
        var decisions = receipt["decisions"]!.AsArray();
        var packet = JsonNode.Parse(await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath))!["pending"]!;
        Assert.Equal(2, sources.Count);
        Assert.Equal(2, decisions.Count);
        for (var index = 0; index < 2; index++)
        {
            Assert.Equal(index + 1, sources[index]!["ordinal"]!.GetValue<int>());
            Assert.Equal(index + 1, sources[index]!["instanceSourceOrdinal"]!.GetValue<int>());
            Assert.Equal(index + 1, decisions[index]!["ordinal"]!.GetValue<int>());
            Assert.True(JsonNode.DeepEquals(packet["sources"]![index], sources[index]!["witness"]));
            Assert.True(JsonNode.DeepEquals(sources[index]!["witness"], decisions[index]!["sourceWitness"]));
            Assert.Equal("none", decisions[index]!["decision"]!.GetValue<string>());
        }
        Assert.Equal(decisions[0]!["binding"]!["continuationGeneration"]!.GetValue<int>(),
            decisions[1]!["binding"]!["continuationGeneration"]!.GetValue<int>());
        Assert.Equal(decisions[0]!["binding"]!["waveOrdinal"]!.GetValue<int>(),
            decisions[1]!["binding"]!["waveOrdinal"]!.GetValue<int>());
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundOpportunityReceiptState.StatePath));
    }

    /// <summary>
    /// Rejects a physical receipt changed after the signed original snapshot before producing a plan.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC3Decline_RejectsChangedReceiptBeforePlanning()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var opened = await adapter.OpenC2PrivateSessionAsync(lease);
        using var offer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var completed = await offer.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = offer.Offer!.OpportunityRef,
                decision = "none"
            }), null);
        Assert.Equal("completed_unpublished", completed.Disposition);
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            SpiritualWoundOpportunityReceiptState.StatePath, "{}"u8.ToArray());

        var rejected = await terminal.ReduceCompletedDeclinesAsync(lease);

        Assert.False(rejected.Success);
        Assert.Null(rejected.Reduction);
        Assert.Null(rejected.ReceiptAfterImage);
        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "spiritual_c3_decline_receipt_changed");
    }
}
