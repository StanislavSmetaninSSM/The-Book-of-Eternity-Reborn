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

    [Theory]
    [InlineData("receipt")]
    [InlineData("conflict")]
    [InlineData("both")]
    public void Resolve_ReusesAcceptedInstanceFromPublishedUtf8BomBytes(string location)
    {
        var bytes = Encoding.UTF8.GetBytes(SpiritualWoundOpportunityReceiptStateTests.TwoSides().ToJsonString());
        var baseline = SpiritualWoundFirstOfferInstanceResolver.Resolve(new(true, bytes), Conflict,
            "chaos_sea", "session-b", "request-b", "snapshot-b", 43);
        Assert.Empty(baseline.Issues);
        var receipt = new CanonicalBeforeImage(true, location is "receipt" or "both"
            ? Encoding.UTF8.GetPreamble().Concat(bytes).ToArray() : bytes);
        var conflict = new CanonicalBeforeImage(true, location is "conflict" or "both"
            ? Encoding.UTF8.GetPreamble().Concat(Conflict.Bytes!).ToArray() : Conflict.Bytes);
        var fingerprints = (receipt.Fingerprint, conflict.Fingerprint);
        var result = SpiritualWoundFirstOfferInstanceResolver.Resolve(receipt, conflict,
            "chaos_sea", "session-b", "request-b", "snapshot-b", 43);
        Assert.Empty(result.Issues);
        Assert.Equal(baseline.InstanceId, result.InstanceId);
        Assert.Null(result.ProposedRow);
        Assert.Equal(fingerprints, (receipt.Fingerprint, conflict.Fingerprint));
    }

    [Theory]
    [InlineData("receipt", "invalid-utf8")]
    [InlineData("conflict", "invalid-utf8")]
    [InlineData("receipt", "double-bom")]
    [InlineData("conflict", "double-bom")]
    public void Resolve_RefusesMalformedSignedBytesWithoutLeakingDecoderExceptions(string location, string corruption)
    {
        var receiptBytes = Encoding.UTF8.GetBytes(SpiritualWoundOpportunityReceiptStateTests.TwoSides().ToJsonString());
        var original = location == "receipt" ? receiptBytes : Conflict.Bytes!;
        var invalid = corruption == "double-bom"
            ? Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetPreamble()).Concat(original).ToArray()
            : Encoding.UTF8.GetBytes("{\"note\":\"").Concat(new byte[] { 0xff })
                .Concat(Encoding.UTF8.GetBytes("\",")).Concat(original.Skip(1)).ToArray();
        var receipt = new CanonicalBeforeImage(true, location == "receipt" ? invalid : receiptBytes);
        var conflict = new CanonicalBeforeImage(true, location == "conflict" ? invalid : Conflict.Bytes);
        var fingerprints = (receipt.Fingerprint, conflict.Fingerprint);
        var result = SpiritualWoundFirstOfferInstanceResolver.Resolve(receipt, conflict,
            "chaos_sea", "session-b", "request-b", "snapshot-b", 43);
        Assert.NotEmpty(result.Issues);
        Assert.Null(result.InstanceId);
        Assert.Null(result.ProposedRow);
        Assert.Equal(fingerprints, (receipt.Fingerprint, conflict.Fingerprint));
    }

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

        foreach (var turn in new[] { 42, 43 })
        {
            var result = SpiritualWoundFirstOfferInstanceResolver.Resolve(
                new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(receipt.ToJsonString())),
                Conflict, "chaos_sea", "session-a", "request-42", "snapshot-" + turn, turn);
            Assert.Null(result.InstanceId);
            Assert.Null(result.ProposedRow);
            Assert.Contains(result.Issues,
                issue => issue.Code == "spiritual_first_offer_already_accepted");
        }
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
