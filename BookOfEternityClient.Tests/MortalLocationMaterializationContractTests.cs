using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLocationMaterializationContractTests
{
    private const string Context = "worldMapUpdates.newLocations[0]";
    private const string TargetLocationId = "loc_test_watchtower";

    [Fact]
    public void ValidateRawLocation_CompleteObject_ReturnsNoIssues()
    {
        using var document = Parse(MortalLocationTestFixture.CreateRawLocation());

        Assert.Empty(MortalLocationMaterializationContract.ValidateRawLocation(
            document.RootElement,
            Context,
            "world_map_creation"));
    }

    [Fact]
    public void ValidateCanonicalLocation_CompleteObject_ReturnsNoIssues()
    {
        using var document = Parse(MortalLocationTestFixture.CreateCanonicalLocation());

        Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLocation(
            document.RootElement,
            "world_map.locations[0]"));
    }

    [Theory]
    [InlineData("schemaVersion", "mortal_location_materialization_invalid_envelope")]
    [InlineData("entityKind", "mortal_location_materialization_invalid_envelope")]
    [InlineData("realm", "mortal_location_materialization_wrong_realm")]
    [InlineData("state", "mortal_location_materialization_invalid_envelope")]
    [InlineData("sourceTurn", "mortal_location_materialization_invalid_envelope")]
    public void ValidateRawLocation_RejectsClosedEnvelopeValue(
        string field,
        string expectedCode)
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        var envelope = location["materialization"]!.AsObject();
        envelope[field] = field switch
        {
            "schemaVersion" => 2,
            "entityKind" => "location",
            "realm" => "shining_abode",
            "state" => "partial",
            "sourceTurn" => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        using var document = Parse(location);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                Context,
                "world_map_creation"),
            issue => issue.Code == expectedCode);
    }

    [Fact]
    public void ValidateRawLocation_RequiresExpectedRouteAndAuthority()
    {
        var location = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        location["materialization"]!["sourceAuthority"]!["kind"] = "npc";

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateRawLocation(
            document.RootElement,
            Context,
            "world_map_creation");

        Assert.Contains(issues, issue => issue.Code == "mortal_location_materialization_route_mismatch");
        Assert.Contains(issues, issue => issue.Code == "mortal_location_materialization_source_authority_mismatch");
    }

    [Theory]
    [InlineData("envelope")]
    [InlineData("sourceAuthority")]
    [InlineData("sections")]
    [InlineData("disposition")]
    public void ValidateRawLocation_RejectsUnknownClosedMember(string target)
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        var envelope = location["materialization"]!.AsObject();
        switch (target)
        {
            case "envelope":
                envelope["futureField"] = true;
                break;
            case "sourceAuthority":
                envelope["sourceAuthority"]!["futureField"] = true;
                break;
            case "sections":
                envelope["sections"]!["futureSection"] = new JsonObject
                {
                    ["disposition"] = "populated",
                    ["reason"] = null
                };
                break;
            case "disposition":
                envelope["sections"]!["presentation"]!["futureField"] = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target));
        }

        using var document = Parse(location);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                Context,
                "world_map_creation"),
            issue => issue.Code == "mortal_location_materialization_unknown_field");
    }

    [Theory]
    [InlineData("receiptId")]
    [InlineData("seal")]
    [InlineData("locationIdentityIndex")]
    [InlineData("requestId")]
    [InlineData("sessionId")]
    public void ValidateRawLocation_RejectsGmAuthoredClientField(string field)
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        location[field] = "forged";

        using var document = Parse(location);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                Context,
                "world_map_creation"),
            issue => issue.Code == "mortal_location_materialization_gm_authored_client_field");
    }

    [Theory]
    [InlineData("features")]
    [InlineData("eventDescriptions")]
    [InlineData("factionControl")]
    [InlineData("actorBindings")]
    [InlineData("locationStorages")]
    [InlineData("activeThreats")]
    [InlineData("loreBindings")]
    [InlineData("customStates")]
    public void ValidateRawLocation_RequiresPhysicalGovernedArrays(string field)
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        location.Remove(field);

        using var document = Parse(location);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                Context,
                "world_map_creation"),
            issue => issue.Code == "mortal_location_materialization_governed_field_missing" &&
                     issue.FilePath.EndsWith(field, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("outdoor", "biome", null)]
    [InlineData("outdoor", "indoorType", "cave")]
    [InlineData("indoor", "indoorType", null)]
    [InlineData("indoor", "biome", "riverlands")]
    public void ValidateRawLocation_RejectsIndoorOutdoorShape(
        string locationType,
        string field,
        string? replacement)
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["locationType"] = locationType;
        if (locationType == "indoor")
        {
            location["biome"] = null;
            location["biomeDescription"] = null;
            location["indoorType"] = "tower";
        }
        location[field] = replacement;

        using var document = Parse(location);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                Context,
                "world_map_creation"),
            issue => issue.Code == "mortal_location_materialization_physical_shape_invalid");
    }

    [Theory]
    [InlineData("hidden", "gm_only", null, true)]
    [InlineData("rumored", "player_known", "Говорят о холодной переправе.", true)]
    [InlineData("discovered", "player_known", null, true)]
    [InlineData("visited", "player_known", null, true)]
    [InlineData("hidden", "player_known", null, false)]
    [InlineData("rumored", "player_known", null, false)]
    [InlineData("visited", "gm_only", null, false)]
    public void ValidateRawLocation_EnforcesDiscoveryPair(
        string tier,
        string audience,
        string? rumorSummary,
        bool valid)
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["discovery"] = new JsonObject
        {
            ["tier"] = tier,
            ["audience"] = audience,
            ["rumorSummary"] = rumorSummary
        };

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateRawLocation(
            document.RootElement,
            Context,
            "world_map_creation");

        Assert.Equal(valid, issues.All(issue => issue.Code != "mortal_location_materialization_discovery_invalid"));
    }

    [Fact]
    public void ValidateRawLocation_EmptySectionRequiresReasonAndPhysicalEmptyValue()
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["factionControl"] = new JsonArray(new JsonObject { ["factionId"] = "fac_test" });
        location["materialization"]!["sections"]!["actorBindings"]!["reason"] = "   ";

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateRawLocation(
            document.RootElement,
            Context,
            "world_map_creation");

        Assert.Contains(issues, issue => issue.Code == "mortal_location_materialization_section_disposition_mismatch");
        Assert.Contains(issues, issue => issue.Code == "mortal_location_materialization_section_empty_reason_missing");
    }

    [Fact]
    public void ValidateRawLocation_DuplicatePropertyIsRejectedBeforeNodeConversion()
    {
        var json = MortalLocationTestFixture.CreateRawLocation().ToJsonString()
            .Replace(
                "\"realm\":\"mortal_world\"",
                "\"realm\":\"mortal_world\",\"realm\":\"mortal_world\"",
                StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);

        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                Context,
                "world_map_creation"),
            issue => issue.Code == "mortal_location_materialization_duplicate_property");
    }

    [Fact]
    public void ValidateCanonicalLocation_ReceiptlessObjectIsRejected()
    {
        using var document = Parse(MortalLocationTestFixture.CreateReceiptlessNegative());

        Assert.Contains(
            MortalLocationMaterializationContract.ValidateCanonicalLocation(
                document.RootElement,
                "world_map.locations[0]"),
            issue => issue.Code == "mortal_location_materialization_receipt_required");
    }

    [Theory]
    [InlineData("locationId", "loc_other")]
    [InlineData("materializationId", "mlocmat_other")]
    [InlineData("route", "current_scene_creation")]
    [InlineData("sourceAuthorityId", "turn_other")]
    public void ValidateCanonicalLocation_ReceiptMustMatchEnvelopeAndRoot(
        string receiptField,
        string replacement)
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        location["materializationReceipt"]![receiptField] = replacement;

        using var document = Parse(location);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateCanonicalLocation(
                document.RootElement,
                "world_map.locations[0]"),
            issue => issue.Code == "mortal_location_materialization_receipt_mismatch");
    }

    [Fact]
    public void ValidateRawLink_CompleteObject_ReturnsNoIssues()
    {
        using var document = Parse(MortalLocationTestFixture.CreateRawLink(
            MortalLocationTestFixture.LocationId,
            TargetLocationId));

        Assert.Empty(MortalLocationMaterializationContract.ValidateRawLink(
            document.RootElement,
            "worldMapUpdates.newLinks[0]",
            "world_map_link_creation"));
    }

    [Fact]
    public void ValidateCanonicalLink_CompleteObject_ReturnsNoIssues()
    {
        using var document = Parse(MortalLocationTestFixture.CreateCanonicalLink(
            MortalLocationTestFixture.LocationId,
            TargetLocationId));

        Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLink(
            document.RootElement,
            "world_map.links[0]"));
    }

    [Theory]
    [InlineData("source", false, false)]
    [InlineData("source", true, true)]
    [InlineData("target", false, false)]
    [InlineData("target", true, true)]
    public void ValidateRawLink_RequiresEndpointSelectorXor(
        string endpoint,
        bool permanentPresent,
        bool temporaryPresent)
    {
        var link = MortalLocationTestFixture.CreateRawLink(
            MortalLocationTestFixture.LocationId,
            TargetLocationId);
        link[$"{endpoint}LocationId"] = permanentPresent
            ? $"loc_{endpoint}"
            : null;
        link[$"{endpoint}InitialId"] = temporaryPresent
            ? $"locref_{endpoint}"
            : null;

        using var document = Parse(link);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLink(
                document.RootElement,
                "worldMapUpdates.newLinks[0]",
                "world_map_link_creation"),
            issue => issue.Code == "mortal_location_link_endpoint_selector_invalid");
    }

    [Fact]
    public void ValidateCanonicalLink_RejectsTemporaryEndpointAndReceiptMismatch()
    {
        var link = MortalLocationTestFixture.CreateCanonicalLink(
            MortalLocationTestFixture.LocationId,
            TargetLocationId);
        link["targetInitialId"] = "locref_forged";
        link["materializationReceipt"]!["targetLocationId"] = "loc_other";

        using var document = Parse(link);
        var issues = MortalLocationMaterializationContract.ValidateCanonicalLink(
            document.RootElement,
            "world_map.links[0]");

        Assert.Contains(issues, issue => issue.Code == "mortal_location_link_endpoint_selector_invalid");
        Assert.Contains(issues, issue => issue.Code == "mortal_location_materialization_receipt_mismatch");
    }

    private static JsonDocument Parse(JsonObject value) =>
        JsonDocument.Parse(value.ToJsonString());
}
