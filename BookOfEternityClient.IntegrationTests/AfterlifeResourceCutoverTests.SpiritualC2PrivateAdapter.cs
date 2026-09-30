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
    /// Refuses to fabricate a GM offer when no original checkpoint exists.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PrivateAdapter_BlocksWithoutCheckpoint()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var opened = await adapter.OpenC2PrivateSessionAsync(lease);

        Assert.Equal("blocked", opened.Disposition);
        Assert.Null(opened.Session);
        Assert.Contains(opened.Issues, issue =>
            issue.Code == "spiritual_c2_private_checkpoint_required");
    }

    /// <summary>
    /// Repairs a missing projection before presenting only the current safe GM offer.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PrivateAdapter_RepairsAndOffersSafeCurrentSource()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        context.FileSystem.DeleteFile(lease, SpiritualWoundDecisionPendingState.StatePath);
        var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var opened = await adapter.OpenC2PrivateSessionAsync(lease);

        Assert.Equal("offer", opened.Disposition);
        AssertNoConflictFrameErrors(opened.Issues);
        using var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var offer = Assert.IsType<ValidationService.SpiritualC2PrivateOffer>(session.Offer);
        Assert.StartsWith("spiritual_wound_", offer.OpportunityRef);
        Assert.Equal(1, offer.MaximumSeverityRank);
        Assert.Equal(new[] { "none", "materialize" }, offer.AllowedDecisions);
        Assert.Contains("spiritual_axis", offer.AllowedLocationKinds);
        var serialized = JsonSerializer.Serialize(offer);
        foreach (var privateMarker in new[]
                 {
                     "checkpoint", "pending", "sourceId", "fingerprint", "allocation",
                     "candidateAfterImages", "beforeImages", "diceClaims"
                 })
            Assert.DoesNotContain(privateMarker, serialized, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath));
    }

    /// <summary>
    /// Rejects a forged reference before writing and commits only an offered decision.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PrivateAdapter_RejectsForgeryThenCommitsDecline()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var opened = await adapter.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var checkpointBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var commandBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath);

        var forged = await session.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = "spiritual_wound_forged",
                decision = "none"
            }), null);

        Assert.Equal("blocked", forged.Disposition);
        Assert.Equal(checkpointBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(commandBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath));
        var committed = await session.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = session.Offer!.OpportunityRef,
                decision = "none"
            }), null);

        Assert.Equal("completed_unpublished", committed.Disposition);
        AssertNoConflictFrameErrors(committed.Issues);
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(
            committed.Session);
        Assert.Null(terminal.Offer);
        var stale = await session.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = session.Offer!.OpportunityRef,
                decision = "none"
            }), null);
        Assert.Equal("blocked", stale.Disposition);
    }

    /// <summary>
    /// Offers the second harmful side only after saving the first side's exact decision.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PrivateAdapter_AdvancesTwoSourcesWithoutReusingOldOffer()
    {
        await using var context = await CreateTwoSourceC2ContextAsync();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var opened = await adapter.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using var first = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var firstRef = first.Offer!.OpportunityRef;

        var advanced = await first.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = firstRef,
                decision = "none"
            }), null);

        Assert.Equal("offer", advanced.Disposition);
        using var second = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(
            advanced.Session);
        var secondCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(
            OriginalCaptureField(second, "_capture"));
        Assert.Null(OriginalCaptureField(secondCapture, "_completedOrdinaryReduction"));
        Assert.NotEqual(firstRef, second.Offer!.OpportunityRef);
        var duplicate = await first.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = firstRef,
                decision = "none"
            }), null);
        Assert.Equal("blocked", duplicate.Disposition);
        var completed = await second.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = second.Offer.OpportunityRef,
                decision = "none"
            }), null);
        Assert.Equal("completed_unpublished", completed.Disposition);
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(
            completed.Session);
        Assert.Null(terminal.Offer);
        var finalCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(
            OriginalCaptureField(terminal, "_capture"));
        Assert.NotNull(OriginalCaptureField(finalCapture, "_completedOrdinaryReduction"));
        AssertNoConflictFrameErrors((await terminal.ReduceCompletedDecisionsAsync(lease)).Issues);
    }

    /// <summary>
    /// Rejects a different valid physical command inserted between composition and saved draft capture.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PrivateAdapter_BindsSubmittedCommandAgainstReplacement()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var opened = await adapter.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var checkpointBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var swapped = false;

        var result = await session.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = session.Offer!.OpportunityRef,
                decision = "none"
            }), "Исходная сцена.", async held =>
            {
                var original = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(
                    held, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
                original["commands"]![0]!["finalSceneText"] = "Подменённая сцена.";
                await context.FileSystem.WriteFileAtomicBytesAsync(held,
                    AcceptedMechanicsPlan.WoundCommandPath,
                    System.Text.Encoding.UTF8.GetBytes(original.ToJsonString()));
                swapped = true;
            });

        Assert.True(swapped);
        Assert.Equal("blocked", result.Disposition);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "spiritual_c2_submitted_command_changed");
        Assert.Equal(checkpointBefore, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath));
    }

    /// <summary>
    /// Allows one owner-bound submission when two callers race on the same offered session.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2PrivateAdapter_ConcurrentSubmissionConsumesOfferOnce()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var opened = await adapter.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var decision = JsonSerializer.SerializeToElement(new
        {
            opportunityRef = session.Offer!.OpportunityRef,
            decision = "none"
        });

        var results = await Task.WhenAll(
            session.SubmitDecisionAsync(lease, decision, null),
            session.SubmitDecisionAsync(lease, decision, null));

        Assert.Single(results, result => result.Disposition == "completed_unpublished");
        Assert.Single(results, result => result.Disposition == "blocked");
        foreach (var completed in results.Where(result => result.Session is not null))
            completed.Session!.Dispose();
        var checkpoint = JsonNode.Parse(await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath))!["checkpoint"]!;
        Assert.Equal(1, checkpoint["committedAdvance"]!.GetValue<int>());
    }
}
