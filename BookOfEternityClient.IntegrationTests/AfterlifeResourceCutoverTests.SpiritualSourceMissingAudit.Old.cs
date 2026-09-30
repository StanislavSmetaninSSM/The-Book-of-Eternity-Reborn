using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Retains a live source owner without admitting sources or claiming dice when the opposition cost audit is missing.
    /// </summary>
    /// <returns>
    /// A task completing after the owner, pending requirement and absent validation seal are checked.
    /// </returns>
    [Fact]
    public async Task SourceMissingAudit_OldSignedPartialRetainsOwnerWithoutSourceOrDice()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = await ReadSourceMissingCandidateAsync(context);
        root["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(lease);

        var owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(result.Session);
        AssertNoConflictFrameErrors(result.Issues);
        Assert.Empty(owner.Sources);
        Assert.Empty(owner.ClaimedDice);
        Assert.Empty(owner.CheckedExchanges);
        Assert.Single(owner.PendingRequirements);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    private static async Task<JsonObject> ReadSourceMissingCandidateAsync(
        ResourceMaterializationTestContext context)
    {
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var projected = root[AfterlifeSpiritualConflictState.ResponseField] is JsonObject update
            ? AfterlifeSpiritualConflictState.ApplyUpdate(root, update) : root.DeepClone().AsObject();
        projected.Remove(AfterlifeSpiritualConflictState.ResponseField);
        Assert.False(projected.ContainsKey("lastInvalidUpdate"));
        return projected;
    }
}
