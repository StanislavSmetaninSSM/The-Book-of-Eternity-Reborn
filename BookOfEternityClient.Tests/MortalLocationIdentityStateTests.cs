using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLocationIdentityStateTests
{
    [Fact]
    public void Parse_EmptyCurrentRoot_ReturnsDeterministicShape()
    {
        var state = MortalLocationIdentityState.Parse(MortalLocationIdentityState.CreateEmptyRoot());

        Assert.Empty(state.Issues);
        Assert.Empty(state.LocationEntriesById);
        Assert.Empty(state.LinkEntriesById);
        Assert.Equal(0, state.EntriesScanned);
        Assert.True(JsonNode.DeepEquals(MortalLocationIdentityState.CreateEmptyRoot(), state.ToJson()));
    }

    [Fact]
    public void Parse_RejectsUnknownAndMalformedClientState()
    {
        var root = MortalLocationTestFixture.CreateIdentityIndex(
            MortalLocationTestFixture.CreateCanonicalLocation());
        root["futureRootField"] = true;
        root["locationEntries"]![0]!["futureEntryField"] = true;
        root["linkEntries"] = "not-an-array";

        var state = MortalLocationIdentityState.Parse(root);

        Assert.Contains(state.Issues, issue => issue.Code == "mortal_location_identity_unknown_field");
        Assert.Contains(state.Issues, issue => issue.Code == "mortal_location_identity_invalid_index");
    }

    [Fact]
    public void Parse_RawJsonRejectsDuplicateProperty()
    {
        var state = MortalLocationIdentityState.Parse(
            "{\"schemaVersion\":1,\"schemaVersion\":1,\"realm\":\"mortal_world\",\"locationEntries\":[],\"linkEntries\":[]}");

        Assert.Contains(state.Issues, issue => issue.Code == "mortal_location_identity_duplicate_property");
    }

    [Theory]
    [InlineData("locationId", "mortal_location_identity_duplicate_location_id")]
    [InlineData("initialId", "mortal_location_identity_duplicate_origin")]
    [InlineData("materializationId", "mortal_location_identity_duplicate_origin")]
    [InlineData("receiptId", "mortal_location_identity_duplicate_receipt_id")]
    public void Parse_RejectsDuplicateLocationIdentityEvidence(string field, string expectedCode)
    {
        var root = MortalLocationTestFixture.CreateIdentityIndex(
            MortalLocationTestFixture.CreateCanonicalLocation());
        var duplicate = root["locationEntries"]![0]!.DeepClone().AsObject();
        duplicate["locationId"] = "loc_second";
        duplicate["initialId"] = "locref_second";
        duplicate["materializationId"] = "mlocmat_second";
        duplicate["receiptId"] = "mlocrec_second";
        duplicate[field] = root["locationEntries"]![0]![field]!.DeepClone();
        root["locationEntries"]!.AsArray().Add(duplicate);

        var state = MortalLocationIdentityState.Parse(root);

        Assert.Contains(state.Issues, issue => issue.Code == expectedCode);
    }

    [Theory]
    [InlineData("locref_test_black_ford", null)]
    [InlineData(null, "mlocmat_test_black_ford")]
    [InlineData("LOCREF_TEST_BLACK_FORD", null)]
    [InlineData(" locref_test_black_ford ", null)]
    [InlineData("locref_test_blаck_ford", null)]
    public void ContainsHistoricalLocationOrigin_MatchesFieldsIndependentlyAndRejectsConfusables(
        string? initialId,
        string? materializationId)
    {
        var state = MortalLocationIdentityState.Parse(
            MortalLocationTestFixture.CreateIdentityIndex(
                MortalLocationTestFixture.CreateCanonicalLocation()));

        Assert.True(state.ContainsHistoricalLocationOrigin(initialId, materializationId));
    }

    [Fact]
    public void ContainsHistoricalLocationOrigin_MatchesNfcAndNfdVariants()
    {
        var root = MortalLocationTestFixture.CreateIdentityIndex(
            MortalLocationTestFixture.CreateCanonicalLocation());
        root["locationEntries"]![0]!["initialId"] = "locref_café";
        var state = MortalLocationIdentityState.Parse(root);

        Assert.True(state.ContainsHistoricalLocationOrigin("locref_café", null));
    }

    [Fact]
    public void ContainsHistoricalLinkOrigin_IncludesRetiredHistory()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        var link = MortalLocationTestFixture.CreateCanonicalLink(
            MortalLocationTestFixture.LocationId,
            "loc_test_watchtower");
        var root = MortalLocationTestFixture.CreateIdentityIndex(location, link);
        root["linkEntries"]![0]!["state"] = "retired";
        var state = MortalLocationIdentityState.Parse(root);

        Assert.True(state.ContainsHistoricalLinkOrigin(MortalLocationTestFixture.LinkInitialId, null));
        Assert.True(state.ContainsHistoricalLinkOrigin(null, MortalLocationTestFixture.LinkMaterializationId));
        Assert.True(state.ContainsHistoricalLinkOrigin(MortalLocationTestFixture.LinkInitialId.ToUpperInvariant(), null));
    }

    [Fact]
    public void ValidateCanonicalState_AcceptsExactMapReceiptAndIndexAgreement()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        var map = MortalLocationTestFixture.CreateWorldMap(location);
        var state = MortalLocationIdentityState.Parse(
            MortalLocationTestFixture.CreateIdentityIndex(location));

        Assert.Empty(state.ValidateCanonicalState(map));
    }

    [Theory]
    [InlineData("receiptId", "mlocrec_other")]
    [InlineData("materializationId", "mlocmat_other")]
    [InlineData("state", "retired")]
    public void ValidateCanonicalState_RejectsReceiptIndexMismatch(
        string field,
        string replacement)
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        var map = MortalLocationTestFixture.CreateWorldMap(location);
        var index = MortalLocationTestFixture.CreateIdentityIndex(location);
        index["locationEntries"]![0]![field] = replacement;
        var state = MortalLocationIdentityState.Parse(index);

        Assert.Contains(
            state.ValidateCanonicalState(map),
            issue => issue.Code == "mortal_location_identity_canonical_mismatch");
    }

    [Fact]
    public void Parse_IndexingWorkScalesLinearly()
    {
        var small = MortalLocationIdentityState.Parse(CreateIndexWithLocations(256));
        var large = MortalLocationIdentityState.Parse(CreateIndexWithLocations(512));

        Assert.Empty(small.Issues);
        Assert.Empty(large.Issues);
        Assert.Equal(256, small.EntriesScanned);
        Assert.Equal(512, large.EntriesScanned);
        Assert.True(large.EntriesScanned <= small.EntriesScanned * 2.5);
    }

    [Fact]
    public void IdentityFactory_UsesOneInjectedGuidPerClientIdentity()
    {
        var values = new Queue<Guid>(new[]
        {
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444")
        });
        var factory = new MortalLocationIdentityFactory(() => values.Dequeue());

        Assert.Equal("loc_11111111111111111111111111111111", factory.CreateLocationId());
        Assert.Equal("mlocrec_22222222222222222222222222222222", factory.CreateLocationReceiptId());
        Assert.Equal("lnk_33333333333333333333333333333333", factory.CreateLinkId());
        Assert.Equal("mlinkrec_44444444444444444444444444444444", factory.CreateLinkReceiptId());
        Assert.Empty(values);
    }

    private static JsonObject CreateIndexWithLocations(int count)
    {
        var template = MortalLocationTestFixture.CreateIdentityIndex(
            MortalLocationTestFixture.CreateCanonicalLocation())["locationEntries"]![0]!.AsObject();
        var entries = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            var entry = template.DeepClone().AsObject();
            entry["locationId"] = $"loc_scale_{index:D4}";
            entry["initialId"] = $"locref_scale_{index:D4}";
            entry["materializationId"] = $"mlocmat_scale_{index:D4}";
            entry["receiptId"] = $"mlocrec_scale_{index:D4}";
            entry["coordinatesAtCreation"] = new JsonObject
            {
                ["x"] = index,
                ["y"] = 0,
                ["z"] = 0
            };
            entries.Add(entry);
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locationEntries"] = entries,
            ["linkEntries"] = new JsonArray()
        };
    }
}
