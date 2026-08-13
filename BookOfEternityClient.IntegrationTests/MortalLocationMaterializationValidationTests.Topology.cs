using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
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

    [Theory]
    [InlineData("case")]
    [InlineData("whitespace")]
    [InlineData("unicode")]
    public void Topology_ConfusableTemporaryEndpointDoesNotResolve(string aliasKind)
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
            ConfusableAlias(source["initialId"]!.GetValue<string>(), aliasKind),
            target["initialId"]!.GetValue<string>());
        var updates = Updates(source, target);
        updates["newLinks"]!.AsArray().Add(link);

        var result = Build(rawWorldMapUpdates: updates);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_link_endpoint_confusable");
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(result.Issues));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(result.Issues));
    }

    [Theory]
    [InlineData("case")]
    [InlineData("whitespace")]
    [InlineData("unicode")]
    public void Topology_ConfusableSameTurnParentSelectorFailsClosedWithoutRepairPacket(string aliasKind)
    {
        var parent = CreateRawLocation(
            "locref_topology_parent_exact",
            "mlocmat_topology_parent_exact",
            x: 27,
            route: "world_map_creation");
        var child = CreateRawLocation(
            "locref_topology_child_exact",
            "mlocmat_topology_child_exact",
            x: 28,
            route: "world_map_creation");
        child["parentInitialId"] =
            ConfusableAlias(parent["initialId"]!.GetValue<string>(), aliasKind);

        var result = Build(rawWorldMapUpdates: Updates(parent, child));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_materialization_parent_confusable");
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(result.Issues));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(result.Issues));
    }

    [Theory]
    [InlineData("exact")]
    [InlineData("case")]
    [InlineData("whitespace")]
    [InlineData("unicode")]
    public void Topology_RetiredPermanentEndpointFailsClosedWithoutRepairPacket(string aliasKind)
    {
        var retired = MortalLocationTestFixture.CreateCanonicalLocation();
        var retiredIndex = MortalLocationTestFixture.CreateIdentityIndex(retired);
        retiredIndex["locationEntries"]![0]!["state"] = "retired";
        var source = CreateRawLocation(
            "locref_topology_retired_endpoint_source",
            "mlocmat_topology_retired_endpoint_source",
            x: 29,
            route: "world_map_creation");
        MarkTopologyPopulated(source);
        var link = CreateRawLink(
            "linkref_topology_retired_endpoint",
            "mlinkmat_topology_retired_endpoint",
            source["initialId"]!.GetValue<string>(),
            "unused_target");
        link["targetInitialId"] = null;
        link["targetLocationId"] = ConfusableAlias(
            retired["locationId"]!.GetValue<string>(),
            aliasKind);
        var updates = Updates(source);
        updates["newLinks"]!.AsArray().Add(link);

        var result = Build(
            preTurnWorldMap: EmptyWorldMap(),
            preTurnIdentityIndex: retiredIndex,
            rawWorldMapUpdates: updates);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_link_endpoint_historical_replay");
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(result.Issues));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(result.Issues));
    }

    [Theory]
    [InlineData("case")]
    [InlineData("whitespace")]
    [InlineData("unicode")]
    public void Topology_ConfusableActivePermanentEndpointFailsClosedWithoutRepairPacket(string aliasKind)
    {
        var canonical = MortalLocationTestFixture.CreateCanonicalLocation();
        var source = CreateRawLocation(
            "locref_topology_active_endpoint_source",
            "mlocmat_topology_active_endpoint_source",
            x: 31,
            route: "world_map_creation");
        MarkTopologyPopulated(source);
        var link = CreateRawLink(
            "linkref_topology_active_endpoint",
            "mlinkmat_topology_active_endpoint",
            source["initialId"]!.GetValue<string>(),
            "unused_target");
        link["targetInitialId"] = null;
        link["targetLocationId"] = ConfusableAlias(
            canonical["locationId"]!.GetValue<string>(),
            aliasKind);
        var updates = Updates(source);
        updates["newLinks"]!.AsArray().Add(link);

        var result = Build(
            preTurnWorldMap: MortalLocationTestFixture.CreateWorldMap(canonical),
            preTurnCurrentLocation: MortalLocationTestFixture.CreateCurrentProjection(canonical),
            preTurnIdentityIndex: MortalLocationTestFixture.CreateIdentityIndex(canonical),
            rawWorldMapUpdates: updates);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_link_endpoint_confusable");
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(result.Issues));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(result.Issues));
    }

    [Theory]
    [InlineData("exact")]
    [InlineData("case")]
    [InlineData("whitespace")]
    [InlineData("unicode")]
    public void Topology_HistoricalTemporaryEndpointFailsClosedWithoutRepairPacket(string aliasKind)
    {
        var retired = MortalLocationTestFixture.CreateCanonicalLocation();
        var retiredIndex = MortalLocationTestFixture.CreateIdentityIndex(retired);
        retiredIndex["locationEntries"]![0]!["state"] = "retired";
        var source = CreateRawLocation(
            "locref_topology_historical_endpoint_source",
            "mlocmat_topology_historical_endpoint_source",
            x: 32,
            route: "world_map_creation");
        MarkTopologyPopulated(source);
        var link = CreateRawLink(
            "linkref_topology_historical_endpoint",
            "mlinkmat_topology_historical_endpoint",
            source["initialId"]!.GetValue<string>(),
            ConfusableAlias(
                retiredIndex["locationEntries"]![0]!["initialId"]!.GetValue<string>(),
                aliasKind));
        var updates = Updates(source);
        updates["newLinks"]!.AsArray().Add(link);

        var result = Build(
            preTurnWorldMap: EmptyWorldMap(),
            preTurnIdentityIndex: retiredIndex,
            rawWorldMapUpdates: updates);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_link_endpoint_historical_replay");
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(result.Issues));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(result.Issues));
    }

    [Theory]
    [InlineData("exact")]
    [InlineData("case")]
    [InlineData("whitespace")]
    [InlineData("unicode")]
    public void Topology_RetiredParentOriginFailsClosedWithoutRepairPacket(string aliasKind)
    {
        var retired = MortalLocationTestFixture.CreateCanonicalLocation();
        var retiredIndex = MortalLocationTestFixture.CreateIdentityIndex(retired);
        retiredIndex["locationEntries"]![0]!["state"] = "retired";
        var child = CreateRawLocation(
            "locref_topology_retired_parent_child",
            "mlocmat_topology_retired_parent_child",
            x: 30,
            route: "world_map_creation");
        child["parentInitialId"] = ConfusableAlias(
            retiredIndex["locationEntries"]![0]!["initialId"]!.GetValue<string>(),
            aliasKind);

        var result = Build(
            preTurnWorldMap: EmptyWorldMap(),
            preTurnIdentityIndex: retiredIndex,
            rawWorldMapUpdates: Updates(child));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_materialization_parent_historical_replay");
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(result.Issues));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(result.Issues));
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

    private static string ConfusableAlias(string exact, string aliasKind) => aliasKind switch
    {
        "exact" => exact,
        "case" => exact.ToUpperInvariant(),
        "whitespace" => " " + exact + " ",
        "unicode" => exact.Replace("l", "ℓ", StringComparison.Ordinal),
        _ => throw new ArgumentOutOfRangeException(nameof(aliasKind))
    };

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
