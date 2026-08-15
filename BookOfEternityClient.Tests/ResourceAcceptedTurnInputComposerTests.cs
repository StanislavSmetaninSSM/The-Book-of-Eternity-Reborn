using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceAcceptedTurnInputComposerTests
{
    [Fact]
    public void Parse_MissingRootIsAnExplicitEmptyNoOp()
    {
        var result = ResourceAcceptedTurnInputComposer.Parse(json: null);

        Assert.True(result.IsMissing);
        Assert.True(result.IsValid);
        Assert.Empty(result.DefinitionCreations);
        Assert.Empty(result.CapacityChanges);
        Assert.Empty(result.ResourceChanges);
        Assert.Empty(result.Root);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \r\n")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    public void Parse_PresentMalformedOrWrongRootNeverBecomesMissing(string json)
    {
        var result = ResourceAcceptedTurnInputComposer.Parse(json);

        Assert.False(result.IsMissing);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code is "resource_command_invalid_json" or
                "resource_command_invalid_root");
    }

    [Theory]
    [InlineData("{\"resourceChanges\":[],\"resourceChanges\":[]}")]
    [InlineData("{\"resourceChanges\":[{\"operation\":\"damage\",\"operation\":\"gain\"}]}")]
    [InlineData("{\"resourceChanges\":[],\"future\":[]}")]
    public void Parse_DuplicateOrUnknownPropertiesFailClosed(string json)
    {
        var result = ResourceAcceptedTurnInputComposer.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code is "resource_command_duplicate_property" or
                "resource_command_unknown_field");
    }

    [Fact]
    public void Parse_ComposesAllThreeClosedCommandKindsInGlobalOrdinalOrder()
    {
        var result = ResourceAcceptedTurnInputComposer.Parse(ValidRoot());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var definition = Assert.Single(result.DefinitionCreations);
        var capacity = Assert.Single(result.CapacityChanges);
        var change = Assert.Single(result.ResourceChanges);
        Assert.Equal((1, "mana_v1", "turn_7:resource:1"),
            (definition.CommandOrdinal, definition.DefinitionRef, definition.EventRef));
        Assert.Equal((2, ResourceCapacityOperation.Initialize, "mana_v1"),
            (capacity.CommandOrdinal, capacity.Operation, capacity.ResourceDefinitionRef));
        Assert.Equal((3, ResourceOperation.Spend, 3m),
            (change.CommandOrdinal, change.Operation, change.Amount));
        Assert.Equal(ResourceOwnerKind.Player, change.Target.OwnerKind);
        Assert.Equal("player_current", change.Target.TargetId);
        Assert.Null(change.Target.TargetRef);
        Assert.Equal("narrative_outcome", change.Source.Kind);
        Assert.Equal("exchange_7", change.Source.SourceId);
    }

    [Fact]
    public void Parse_ReturnedDefinitionAndRootAreDefensiveClones()
    {
        var result = ResourceAcceptedTurnInputComposer.Parse(ValidRoot());
        var root = result.Root;
        var definition = Assert.Single(result.DefinitionCreations).Definition;

        root["forged"] = true;
        definition["resourceKey"] = "forged";

        Assert.Null(result.Root["forged"]);
        Assert.Equal(
            "mana",
            Assert.Single(result.DefinitionCreations).Definition["resourceKey"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("{\"kind\":\"player\",\"targetId\":\"player_current\",\"targetRef\":\"player_ref\"}")]
    [InlineData("{\"kind\":\"player\"}")]
    [InlineData("{\"kind\":\"currency\",\"targetId\":\"coins\"}")]
    [InlineData("{\"kind\":\"player\",\"targetId\":\" player_current\"}")]
    [InlineData("{\"kind\":\"player\",\"targetId\":\"player_current\",\"displayName\":\"Hero\"}")]
    public void Parse_RejectsAmbiguousUnregisteredOrInexactTarget(string targetJson)
    {
        var json = $$"""
        {
          "resourceChanges": [{
            "operation": "damage",
            "target": {{targetJson}},
            "resourceKey": "health",
            "amount": 1,
            "source": {"kind":"combat_outcome","sourceId":"exchange_7"},
            "eventRef": "turn_7:resource:1",
            "reason": "Hit"
          }]
        }
        """;

        var result = ResourceAcceptedTurnInputComposer.Parse(json);

        Assert.Contains(result.Issues, issue =>
            issue.Code is "resource_command_target_invalid" or
                "resource_command_unknown_field");
        Assert.Empty(result.ResourceChanges);
    }

    [Theory]
    [InlineData("heal", "1")]
    [InlineData("damage", "0")]
    [InlineData("damage", "-1")]
    [InlineData("damage", "1e1000")]
    [InlineData("damage", "0.123456789012345678901234567891")]
    public void Parse_RejectsUnsupportedOrNonpositiveInexactOrdinaryMutation(
        string operation,
        string amount)
    {
        var json = $$"""
        {
          "resourceChanges": [{
            "operation": "{{operation}}",
            "target": {"kind":"player","targetId":"player_current"},
            "resourceKey": "health",
            "amount": {{amount}},
            "source": {"kind":"combat_outcome","sourceId":"exchange_7"},
            "eventRef": "turn_7:resource:1",
            "reason": "Hit"
          }]
        }
        """;

        var result = ResourceAcceptedTurnInputComposer.Parse(json);

        Assert.False(result.IsValid);
        Assert.Empty(result.ResourceChanges);
    }

    [Theory]
    [InlineData("initialize", null, "preserve")]
    [InlineData("reconfigure", null, "initialize_from_definition")]
    [InlineData("suspend", "{\"kind\":\"instance_fixed\",\"maximum\":10}", null)]
    [InlineData("retire", null, "preserve")]
    public void Parse_RejectsInvalidCapacityLifecycleShape(
        string operation,
        string? capacity,
        string? disposition)
    {
        var json = $$"""
        {
          "resourceCapacityChanges": [{
            "operation": "{{operation}}",
            "target": {"kind":"player","targetId":"player_current"},
            "resourceKey": "health",
            {{(capacity == null ? string.Empty : $"\"capacity\":{capacity},")}}
            {{(disposition == null ? string.Empty : $"\"currentDisposition\":\"{disposition}\",")}}
            "source": {"kind":"owner_lifecycle","sourceId":"player_current"},
            "eventRef": "turn_7:resource:1",
            "reason": "Lifecycle"
          }]
        }
        """;

        var result = ResourceAcceptedTurnInputComposer.Parse(json);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_command_capacity_invalid");
        Assert.Empty(result.CapacityChanges);
    }

    [Fact]
    public void Parse_RejectsDefinitionKeyAndDefinitionRefTogether()
    {
        var root = JsonNode.Parse(ValidRoot())!.AsObject();
        root["resourceCapacityChanges"]![0]!["resourceKey"] = "mana";

        var result = ResourceAcceptedTurnInputComposer.Parse(root.ToJsonString());

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_command_definition_selector_invalid");
        Assert.Empty(result.CapacityChanges);
    }

    [Fact]
    public void Parse_RejectsConfusableDefinitionRefsAndClientOwnedDefinitionState()
    {
        var root = JsonNode.Parse(ValidRoot())!.AsObject();
        var first = root["resourceDefinitionCreations"]![0]!.DeepClone();
        first!["definitionRef"] = "MANA_V1";
        first["definition"]!["materialization"] = new JsonObject
        {
            ["definitionId"] = "forged"
        };
        root["resourceDefinitionCreations"]!.AsArray().Add(first);

        var result = ResourceAcceptedTurnInputComposer.Parse(root.ToJsonString());

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_command_definition_ref_ambiguous");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_client_field_forbidden");
    }

    [Fact]
    public void Parse_EnforcesCapacityAndOrdinaryCommandLimits()
    {
        var capacity = JsonNode.Parse(ValidRoot())!["resourceCapacityChanges"]![0]!;
        var ordinary = JsonNode.Parse(ValidRoot())!["resourceChanges"]![0]!;
        var root = new JsonObject
        {
            ["resourceCapacityChanges"] = Repeat(
                capacity,
                ResourceMaterializationContract.MaxCapacityTransitionsPerTurn + 1),
            ["resourceChanges"] = Repeat(
                ordinary,
                ResourceMaterializationContract.MaxMutationsBeforeTriggers + 1)
        };

        var result = ResourceAcceptedTurnInputComposer.Parse(root.ToJsonString());

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_command_limit_exceeded");
        Assert.Empty(result.CapacityChanges);
        Assert.Empty(result.ResourceChanges);
    }

    [Fact]
    public void BindAcceptedEvents_ComposesExactGlobalCommandOrdinals()
    {
        var commands = ResourceAcceptedTurnInputComposer.Parse(ValidRoot());

        var result = ResourceAcceptedTurnInputComposer.BindAcceptedEvents(
            turn: 7,
            commands);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Equal(
            new[]
            {
                (1, "turn_7:resource:1"),
                (2, "turn_7:resource:2"),
                (3, "turn_7:resource:3")
            },
            result.Events.Select(static value =>
                (value.CommandOrdinal, value.EventRef)));
        Assert.Equal(3, result.Root["events"]!.AsArray().Count);
    }

    [Theory]
    [InlineData("swapped")]
    [InlineData("reused")]
    [InlineData("stale_turn")]
    public void BindAcceptedEvents_RejectsSwappedReusedOrStaleAuthority(
        string mutation)
    {
        var root = JsonNode.Parse(ValidRoot())!.AsObject();
        var turn = 7;
        if (mutation == "swapped")
        {
            root["resourceDefinitionCreations"]![0]!["eventRef"] =
                "turn_7:resource:2";
            root["resourceCapacityChanges"]![0]!["eventRef"] =
                "turn_7:resource:1";
        }
        else if (mutation == "reused")
        {
            root["resourceCapacityChanges"]![0]!["eventRef"] =
                "turn_7:resource:1";
        }
        else
        {
            turn = 8;
        }
        var commands = ResourceAcceptedTurnInputComposer.Parse(root.ToJsonString());
        Assert.True(commands.IsValid, string.Join(Environment.NewLine, commands.Issues));

        var result = ResourceAcceptedTurnInputComposer.BindAcceptedEvents(
            turn,
            commands);

        Assert.False(result.IsValid);
        Assert.Empty(result.Events);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_command_event_authority_mismatch");
    }

    private static JsonArray Repeat(JsonNode value, int count)
    {
        var result = new JsonArray();
        for (var index = 0; index < count; index++)
            result.Add(value.DeepClone());
        return result;
    }

    private static string ValidRoot() => """
    {
      "resourceDefinitionCreations": [{
        "definitionRef": "mana_v1",
        "definition": {
          "resourceKey": "mana",
          "definitionVersion": 1,
          "displayName": "Mana",
          "numericKind": "integer",
          "unit": "point",
          "quantum": 1,
          "minimumPolicy": {"kind":"definition_fixed","value":0},
          "capacityPolicy": {"kind":"instance_fixed"},
          "initializationPolicy": {"kind":"maximum"},
          "allowedOwnerKinds": ["player"],
          "allowedOperations": ["spend","gain"],
          "defaultFloorPolicy": "reject_below_minimum",
          "defaultCapPolicy": "clamp_to_maximum",
          "visibility": "player_visible"
        },
        "eventRef": "turn_7:resource:1",
        "reason": "Setting materialization"
      }],
      "resourceCapacityChanges": [{
        "operation": "initialize",
        "target": {"kind":"player","targetId":"player_current"},
        "resourceDefinitionRef": "mana_v1",
        "capacity": {"kind":"instance_fixed","maximum":40},
        "currentDisposition": "initialize_from_definition",
        "source": {"kind":"setting_materialization","sourceId":"mana_v1"},
        "eventRef": "turn_7:resource:2",
        "reason": "Initialize mana"
      }],
      "resourceChanges": [{
        "operation": "spend",
        "target": {"kind":"player","targetId":"player_current"},
        "resourceKey": "mana",
        "amount": 3,
        "source": {"kind":"narrative_outcome","sourceId":"exchange_7"},
        "eventRef": "turn_7:resource:3",
        "reason": "Cast spell"
      }]
    }
    """;
}
