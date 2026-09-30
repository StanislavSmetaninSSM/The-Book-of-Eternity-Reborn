using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks that the first offer binds an instance to signed history, including signed absence.
/// </summary>
public sealed class SpiritualWoundFirstOfferInstanceResolverTests
{
    private static readonly CanonicalBeforeImage Conflict = new(true,
        Encoding.UTF8.GetBytes("{\"activeConflict\":{\"conflictId\":\"conflict-a\"}}"));

    /// <summary>
    /// Derives a first unpublished instance from signed absence and reuses its accepted row.
    /// </summary>
    [Fact]
    public void Resolve_BindsSignedAbsenceThenExistingAcceptedInstance()
    {
        var first = SpiritualWoundFirstOfferInstanceResolver.Resolve(
            new CanonicalBeforeImage(false, null), Conflict, "chaos_sea",
            "session-a", "request-a", "snapshot-a", 42);

        Assert.Empty(first.Issues);
        var row = Assert.IsType<JsonObject>(first.ProposedRow);
        Assert.Equal(Conflict.Fingerprint, (string?)row["baselineConflictFingerprint"]);
        Assert.Equal(first.InstanceId, (string?)row["instanceId"]);
        Assert.Equal(SpiritualWoundConflictInstanceState.ComputeRowFingerprint(row, "instance"),
            (string?)row["instanceFingerprint"]);

        var receipt = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["nextInstanceOrdinal"] = 2,
            ["nextClosureOrdinal"] = 1,
            ["nextSourceOrdinal"] = 1,
            ["nextDecisionOrdinal"] = 1,
            ["instances"] = new JsonArray(row.DeepClone()),
            ["closures"] = new JsonArray(),
            ["sources"] = new JsonArray(),
            ["decisions"] = new JsonArray()
        };
        var second = SpiritualWoundFirstOfferInstanceResolver.Resolve(
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(receipt.ToJsonString())),
            Conflict, "chaos_sea", "session-b", "request-b", "snapshot-b", 43);

        Assert.Empty(second.Issues);
        Assert.Equal(first.InstanceId, second.InstanceId);
        Assert.Null(second.ProposedRow);
    }

    /// <summary>
    /// Rejects a request whose positive source already appears in signed accepted history.
    /// </summary>
    [Fact]
    public void Resolve_RejectsAlreadyAcceptedOriginalRequest()
    {
        var receipt = SpiritualWoundOpportunityReceiptStateTests.TwoSides();

        var result = SpiritualWoundFirstOfferInstanceResolver.Resolve(
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(receipt.ToJsonString())),
            Conflict, "chaos_sea", "session-a", "request-42", "snapshot-42", 42);

        Assert.Null(result.InstanceId);
        Assert.Null(result.ProposedRow);
        Assert.Contains(result.Issues,
            issue => issue.Code == "spiritual_first_offer_already_accepted");
    }

    /// <summary>
    /// Rejects a new display identity that collides with signed instance history.
    /// </summary>
    [Fact]
    public void Resolve_RejectsConfusableDisplayAlias()
    {
        var receipt = SpiritualWoundOpportunityReceiptStateTests.TwoSides();
        var alias = new CanonicalBeforeImage(true,
            Encoding.UTF8.GetBytes("{\"activeConflict\":{\"conflictId\":\"CONFLICT-A\"}}"));

        var result = SpiritualWoundFirstOfferInstanceResolver.Resolve(
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(receipt.ToJsonString())),
            alias, "chaos_sea", "session-b", "request-b", "snapshot-b", 43);

        Assert.Null(result.InstanceId);
        Assert.Null(result.ProposedRow);
        Assert.Contains(result.Issues,
            issue => issue.Code == "spiritual_first_offer_confusable_display");
    }
}
