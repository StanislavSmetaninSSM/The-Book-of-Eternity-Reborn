using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectRepairPacketBuilderTests
{
    [Fact]
    public void Build_CreatesOneExactFullTurnPacketForSingletonParameterOmission()
    {
        var issue = RepairableIssue();

        var packet = Assert.Single(EffectRepairPacketBuilder.Build(
            new[] { issue },
            rollbackAvailable: true));

        Assert.Equal("effect_materialization_repair", packet.Kind);
        Assert.Equal("effect-apply:effectChanges[0]", packet.Actor);
        Assert.Equal("effectChanges", packet.Route);
        Assert.Equal("effectChanges[0].parameters.intensity", packet.RawCoordinate);
        Assert.Equal(EffectAcceptedTurnPlan.CommandPath, Assert.Single(packet.TargetFiles));
        Assert.Equal("wound", packet.ExpectedSource["kind"]!.GetValue<string>());
        Assert.Equal("wound_test_torn_side", packet.ExpectedSource["sourceId"]!.GetValue<string>());
        Assert.Equal("player", packet.ExpectedTarget["kind"]!.GetValue<string>());
        Assert.Equal(EffectMaterializationTestFixture.DefinitionKey, packet.ExpectedDefinitionKey);
        Assert.Equal("turn_42", packet.ExpectedEventRef["authorityId"]!.GetValue<string>());
        var correction = Assert.Single(packet.ExactFieldCorrections);
        Assert.Equal("effectChanges[0].parameters.intensity", correction.Path);
        Assert.Equal("\"severe\"", correction.ExpectedValueJson);
        Assert.True(packet.FullTurnResubmissionRequired);
        Assert.Contains("effectChanges[0]", packet.ResubmissionObligations);
    }

    [Fact]
    public void ToJsonObject_SerializesOnlyTheClosedEffectRepairContract()
    {
        var packet = Assert.Single(EffectRepairPacketBuilder.Build(
            new[] { RepairableIssue() },
            rollbackAvailable: true));

        var json = packet.ToJsonObject();

        Assert.Equal(
            new[]
            {
                "kind", "actor", "route", "rawCoordinate", "expectedSource",
                "expectedTarget", "expectedDefinitionKey", "expectedEventRef",
                "exactFieldCorrections", "targetFiles",
                "fullTurnResubmissionRequired", "resubmissionObligations"
            },
            json.Select(static pair => pair.Key).ToArray());
        Assert.False(json.ContainsKey("effectId"));
        Assert.False(json.ContainsKey("stackKey"));
        Assert.False(json.ContainsKey("receipt"));
        Assert.False(json.ContainsKey("history"));
        Assert.False(json.ContainsKey("woundId"));
    }

    [Theory]
    [InlineData("effect_identity_duplicate_effect_id")]
    [InlineData("effect_source_selector_unresolved")]
    [InlineData("effect_source_selector_historical")]
    [InlineData("effect_source_selector_confusable")]
    [InlineData("effect_source_realm_mismatch")]
    [InlineData("effect_target_authority_duplicate_target")]
    [InlineData("effect_target_authority_confusable_target")]
    [InlineData("effect_stack_coordinate_conflict")]
    [InlineData("effect_receipt_replay")]
    [InlineData("effect_lifecycle_event_replay")]
    [InlineData("effect_materialization_direct_carrier_mutation")]
    [InlineData("effect_source_wound_link_mismatch")]
    public void Build_RejectsProtectedAuthorityClasses(string code)
    {
        var issue = RepairableIssue(code: code);

        Assert.Empty(EffectRepairPacketBuilder.Build(
            new[] { issue },
            rollbackAvailable: true));
        Assert.True(EffectRepairPacketBuilder.RequiresFailClosedRollback(
            new[] { issue },
            rollbackAvailable: true));
    }

    [Theory]
    [InlineData(EffectIdentityState.StatePath)]
    [InlineData(ResourcePendingResolutionState.PendingPath)]
    [InlineData("game_state/player/effects.json")]
    [InlineData("game_state/player/wounds.json")]
    [InlineData("game_state/resources/resource_state.json")]
    [InlineData("game_state/control/validation_repair_request.json")]
    public void Build_RejectsEveryProtectedOrNonCommandRepairTarget(string target)
    {
        var issue = RepairableIssue(repairTargetFiles: new[] { target });

        Assert.Empty(EffectRepairPacketBuilder.Build(
            new[] { issue },
            rollbackAvailable: true));
        Assert.True(EffectRepairPacketBuilder.RequiresFailClosedRollback(
            new[] { issue },
            rollbackAvailable: true));
    }

    [Fact]
    public void Build_RejectsActionableCandidateWithoutRollbackCapability()
    {
        var issue = RepairableIssue();

        Assert.Empty(EffectRepairPacketBuilder.Build(
            new[] { issue },
            rollbackAvailable: false));
        Assert.True(EffectRepairPacketBuilder.RequiresFailClosedRollback(
            new[] { issue },
            rollbackAvailable: false));
    }

    [Fact]
    public void Build_RejectsTwoSemanticOmissionsEvenOnOneOperation()
    {
        var first = RepairableIssue();
        var second = RepairableIssue(
            rawCoordinate: "effectChanges[0].parameters.severity",
            expectedValueJson: "\"critical\"");

        Assert.Empty(EffectRepairPacketBuilder.Build(
            new[] { first, second },
            rollbackAvailable: true));
        Assert.True(EffectRepairPacketBuilder.RequiresFailClosedRollback(
            new[] { first, second },
            rollbackAvailable: true));
    }

    [Fact]
    public void Build_RejectsAmbiguousOperationBinding()
    {
        var first = RepairableIssue();
        var second = RepairableIssue(
            actor: "effect-apply:effectChanges[1]",
            rawCoordinate: "effectChanges[1].parameters.intensity");

        Assert.Empty(EffectRepairPacketBuilder.Build(
            new[] { first, second },
            rollbackAvailable: true));
        Assert.True(EffectRepairPacketBuilder.RequiresFailClosedRollback(
            new[] { first, second },
            rollbackAvailable: true));
    }

    [Theory]
    [InlineData("effectChanges[0].source.parameters.intensity")]
    [InlineData("effectChanges[1].parameters.intensity")]
    [InlineData("effectChanges[0].parameters.Intensity")]
    public void Build_RejectsContextWhoseRawCoordinateDoesNotMatchTheIssue(
        string contextCoordinate)
    {
        var issue = RepairableIssue(contextRawCoordinate: contextCoordinate);

        Assert.Empty(EffectRepairPacketBuilder.Build(
            new[] { issue },
            rollbackAvailable: true));
        Assert.True(EffectRepairPacketBuilder.RequiresFailClosedRollback(
            new[] { issue },
            rollbackAvailable: true));
    }

    [Fact]
    public void Build_RejectsMissingExactCorrectionEvidence()
    {
        var issue = RepairableIssue(expectedValueJson: null);

        Assert.Empty(EffectRepairPacketBuilder.Build(
            new[] { issue },
            rollbackAvailable: true));
        Assert.True(EffectRepairPacketBuilder.RequiresFailClosedRollback(
            new[] { issue },
            rollbackAvailable: true));
    }

    [Fact]
    public void Build_IgnoresUnrelatedIssuesButNeverUsesThemAsEffectAuthority()
    {
        var unrelated = new ValidationIssue(
            "game_state/quests/regular_quests.json.UpdateQuests[0].description",
            IssueSeverity.Error,
            "Quest description is missing.",
            code: "quest_description_missing",
            section: "Quest");

        Assert.Empty(EffectRepairPacketBuilder.Build(
            new[] { unrelated },
            rollbackAvailable: true));
        Assert.False(EffectRepairPacketBuilder.RequiresFailClosedRollback(
            new[] { unrelated },
            rollbackAvailable: true));
    }

    private static ValidationIssue RepairableIssue(
        string code = "effect_source_parameter_required",
        string actor = "effect-apply:effectChanges[0]",
        string rawCoordinate = "effectChanges[0].parameters.intensity",
        string? contextRawCoordinate = null,
        string? expectedValueJson = "\"severe\"",
        IReadOnlyList<string>? repairTargetFiles = null)
    {
        var issue = new ValidationIssue(
            rawCoordinate,
            IssueSeverity.Error,
            "Required exact source parameter is missing.",
            code: code,
            actor: actor,
            section: "effect_materialization",
            expected: "one exact source-owned value",
            actual: "missing",
            repairHint: "Resubmit the complete turn with this exact bounded value.",
            repairTargetFiles: repairTargetFiles ?? new[]
            {
                EffectAcceptedTurnPlan.CommandPath
            });
        issue.EffectRepairContext = new EffectRepairContext(
            actor,
            "effectChanges",
            contextRawCoordinate ?? rawCoordinate,
            new JsonObject
            {
                ["kind"] = "wound",
                ["sourceId"] = "wound_test_torn_side",
                ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
            },
            new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            EffectMaterializationTestFixture.DefinitionKey,
            new JsonObject
            {
                ["kind"] = "accepted_turn",
                ["authorityId"] = "turn_42"
            },
            expectedValueJson);
        return issue;
    }
}
