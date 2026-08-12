using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLocationMaterializationValidationTests
{
    [Theory]
    [InlineData("road")]
    [InlineData("path")]
    [InlineData("passage")]
    [InlineData("portal")]
    [InlineData("one_way")]
    [InlineData("hidden_path")]
    [InlineData("sealed_passage")]
    public void Topology_DirectedLinkTypesCreateExactlyOnePermanentEdge(string linkType)
    {
        var source = CreateRawLocation(
            "locref_topology_source_" + linkType,
            "mlocmat_topology_source_" + linkType,
            x: 20,
            route: "world_map_creation");
        var target = CreateRawLocation(
            "locref_topology_target_" + linkType,
            "mlocmat_topology_target_" + linkType,
            x: 21,
            route: "world_map_creation");
        MarkTopologyPopulated(source, target);
        var link = CreateRawLink(
            "linkref_topology_" + linkType,
            "mlinkmat_topology_" + linkType,
            sourceInitialId: source["initialId"]!.GetValue<string>(),
            targetInitialId: target["initialId"]!.GetValue<string>());
        link["linkType"] = linkType;
        if (linkType == "hidden_path")
        {
            link["discovery"] = Discovery("hidden");
        }
        if (linkType == "sealed_passage")
        {
            link["access"] = new JsonObject
            {
                ["state"] = "sealed",
                ["reason"] = "Проход запечатан древней печатью.",
                ["requirements"] = new JsonArray()
            };
        }

        var updates = Updates(source, target);
        updates["newLinks"]!.AsArray().Add(link);
        var result = Build(rawWorldMapUpdates: updates);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<BookOfEternityClient.Services.MortalLocationAcceptedTurnPlan>(result.Plan);
        var accepted = Assert.Single(plan.FinalWorldMap["links"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(linkType, accepted["linkType"]!.GetValue<string>());
        Assert.Equal(plan.LinkIdsByInitialId[link["initialId"]!.GetValue<string>()], accepted["linkId"]!.GetValue<string>());
        Assert.DoesNotContain(plan.FinalWorldMap["links"]!.AsArray().OfType<JsonObject>(), candidate =>
            candidate["sourceLocationId"]!.GetValue<string>() == accepted["targetLocationId"]!.GetValue<string>() &&
            candidate["targetLocationId"]!.GetValue<string>() == accepted["sourceLocationId"]!.GetValue<string>());
    }

    [Fact]
    public void Topology_PopulatedDispositionWithoutAcceptedLinkFailsClosed()
    {
        var location = CreateRawLocation(
            "locref_topology_missing_link",
            "mlocmat_topology_missing_link",
            x: 22,
            route: "world_map_creation");
        MarkTopologyPopulated(location);

        var result = Build(rawWorldMapUpdates: Updates(location));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_materialization_topology_disposition_mismatch");
    }

    [Fact]
    public void Topology_EmptyDispositionWithAcceptedLinkFailsClosed()
    {
        var source = CreateRawLocation(
            "locref_topology_empty_source",
            "mlocmat_topology_empty_source",
            x: 23,
            route: "world_map_creation");
        var target = CreateRawLocation(
            "locref_topology_empty_target",
            "mlocmat_topology_empty_target",
            x: 24,
            route: "world_map_creation");
        MarkTopologyPopulated(source);
        var link = CreateRawLink(
            "linkref_topology_empty_target",
            "mlinkmat_topology_empty_target",
            source["initialId"]!.GetValue<string>(),
            target["initialId"]!.GetValue<string>());
        var updates = Updates(source, target);
        updates["newLinks"]!.AsArray().Add(link);

        var result = Build(rawWorldMapUpdates: updates);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_materialization_topology_disposition_mismatch");
    }

    [Fact]
    public void Topology_ConfusableTemporaryEndpointDoesNotResolve()
    {
        var source = CreateRawLocation(
            "locref_topology_case_source",
            "mlocmat_topology_case_source",
            x: 25,
            route: "world_map_creation");
        var target = CreateRawLocation(
            "locref_topology_case_target",
            "mlocmat_topology_case_target",
            x: 26,
            route: "world_map_creation");
        MarkTopologyPopulated(source, target);
        var link = CreateRawLink(
            "linkref_topology_case",
            "mlinkmat_topology_case",
            source["initialId"]!.GetValue<string>().ToUpperInvariant(),
            target["initialId"]!.GetValue<string>());
        var updates = Updates(source, target);
        updates["newLinks"]!.AsArray().Add(link);

        var result = Build(rawWorldMapUpdates: updates);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "mortal_location_link_endpoint_unresolved");
    }

    [Fact]
    public void Topology_PreTurnSelfParentFailsClosed()
    {
        var canonical = MortalLocationTestFixture.CreateCanonicalLocation();
        canonical["parentLocationId"] = MortalLocationTestFixture.LocationId;

        var result = Build(
            preTurnWorldMap: MortalLocationTestFixture.CreateWorldMap(canonical),
            preTurnCurrentLocation: MortalLocationTestFixture.CreateCurrentProjection(canonical),
            preTurnIdentityIndex: MortalLocationTestFixture.CreateIdentityIndex(canonical));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_materialization_parent_cycle");
    }

    private static JsonObject CreateRawLink(
        string initialId,
        string materializationId,
        string sourceInitialId,
        string targetInitialId)
    {
        var link = MortalLocationTestFixture.CreateRawLink("unused_source", "unused_target");
        link["initialId"] = initialId;
        link["sourceLocationId"] = null;
        link["sourceInitialId"] = sourceInitialId;
        link["targetLocationId"] = null;
        link["targetInitialId"] = targetInitialId;
        link["materialization"]!["initialId"] = initialId;
        link["materialization"]!["materializationId"] = materializationId;
        return link;
    }

    private static void MarkTopologyPopulated(params JsonObject[] locations)
    {
        foreach (var location in locations)
        {
            location["materialization"]!["sections"]!["topology"] = new JsonObject
            {
                ["disposition"] = "populated",
                ["reason"] = null
            };
        }
    }

    private static JsonObject Discovery(string tier) => tier switch
    {
        "hidden" => new JsonObject
        {
            ["tier"] = "hidden",
            ["audience"] = "gm_only",
            ["rumorSummary"] = null
        },
        "rumored" => new JsonObject
        {
            ["tier"] = "rumored",
            ["audience"] = "player_known",
            ["rumorSummary"] = "Ходят слухи об этом месте."
        },
        _ => new JsonObject
        {
            ["tier"] = tier,
            ["audience"] = "player_known",
            ["rumorSummary"] = null
        }
    };
}
