using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceRepairPacketBuilderTests
{
    [Fact]
    public void Build_OneExactGmOwnedMissingReasonProducesBoundedFullTurnPacket()
    {
        var issue = CreateIssue(
            "resourceChanges[0].reason",
            "resource_command_invalid_field",
            actual: "missing");

        var packet = Assert.Single(ResourceRepairPacketBuilder.Build(new[] { issue }));

        Assert.Equal("resource_semantic_omission_repair", packet.Kind);
        Assert.Equal("resourceChanges", packet.Route);
        Assert.Equal("ordinary resource change #1", packet.CommandSemantic);
        Assert.Equal(new[] { "narrative reason" }, packet.MissingSemantics);
        Assert.True(packet.FullTurnResubmissionRequired);
        Assert.Empty(packet.TargetFiles);

        var json = JsonSerializer.Serialize(packet.ToJsonObject());
        Assert.DoesNotContain("resourceChanges[0]", json, StringComparison.Ordinal);
        Assert.DoesNotContain("game_state", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resource_state", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("targetId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eventRef", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fingerprint", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("game_state/resources/resource_state.json", "resource_materialization_direct_state_mutation", "changed")]
    [InlineData("game_state/resources/resource_history.json", "resource_history_replay_conflict", "reused")]
    [InlineData("game_state/resources/resource_identity_index.json", "resource_identity_duplicate_transition_id", "duplicate")]
    [InlineData("resourceChanges[0].amount", "resource_arithmetic_overflow", "overflow")]
    [InlineData("resourceChanges[0].target", "resource_owner_unknown", "unknown")]
    [InlineData("resourceCapacityChanges[0]", "resource_capacity_transition_invalid", "invalid")]
    [InlineData("resourceChanges[0].eventRef", "resource_cycle_invalid", "stale")]
    [InlineData("resourceResolutionReceipts[0]", "resource_pending_receipt_invalid", "invalid")]
    [InlineData("resourceChanges[0].target", "resource_command_target_invalid", "missing")]
    [InlineData("resourceChanges[0].source", "resource_command_source_invalid", "missing")]
    [InlineData("resourceChanges[0].eventRef", "resource_command_invalid_field", "missing")]
    [InlineData("resourceChanges[0].amount", "resource_command_amount_invalid", "missing")]
    public void Build_ProtectedOrUnboundedIssueProducesNoActionablePacket(
        string path,
        string code,
        string actual)
    {
        var issue = CreateIssue(path, code, actual);

        Assert.Empty(ResourceRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(ResourceRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Fact]
    public void Build_MultipleSemanticOmissionsFailClosedInsteadOfBroadeningRepair()
    {
        var issues = new[]
        {
            CreateIssue("resourceChanges[0].reason", "resource_command_invalid_field", "missing"),
            CreateIssue("resourceChanges[1].reason", "resource_command_invalid_field", "missing")
        };

        Assert.Empty(ResourceRepairPacketBuilder.Build(issues));
        Assert.True(ResourceRepairPacketBuilder.RequiresFailClosedRollback(issues));
    }

    [Fact]
    public void Build_DuplicateEvidenceForTheSameOmissionStillProducesOnePacket()
    {
        var issue = CreateIssue(
            "resourceChanges[0].reason",
            "resource_command_invalid_field",
            "missing");

        var packet = Assert.Single(ResourceRepairPacketBuilder.Build(new[] { issue, issue }));

        Assert.Equal("ordinary resource change #1", packet.CommandSemantic);
        Assert.False(ResourceRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue, issue }));
    }

    [Fact]
    public void Build_LookalikeMissingReasonWithoutExactProducerEvidenceFailsClosed()
    {
        var issues = new[]
        {
            CreateIssue(
                "resourceChanges[0].reason",
                "resource_command_invalid_field",
                "missing",
                severity: IssueSeverity.Warning),
            CreateIssue(
                "resourceChanges[0].reason",
                "resource_command_invalid_field",
                "missing",
                actor: "GM"),
            CreateIssue(
                "resourceChanges[0].reason",
                "resource_command_invalid_field",
                "missing",
                section: "resource_planner"),
            CreateIssue(
                "resourceChanges[0].reason",
                "resource_command_invalid_field",
                "missing",
                category: IssueCategory.ProtocolViolation),
            CreateIssue(
                "resourceChanges[0].reason",
                "resource_command_invalid_field",
                "missing",
                repairTargetFiles: new[] { ResourceMaterializationContract.CommandPath })
        };

        foreach (var issue in issues)
        {
            Assert.Empty(ResourceRepairPacketBuilder.Build(new[] { issue }));
            Assert.True(ResourceRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
        }
    }

    private static ValidationIssue CreateIssue(
        string path,
        string code,
        string actual,
        IssueSeverity severity = IssueSeverity.Error,
        string actor = "Client",
        string section = "UnifiedResourceAuthority",
        IssueCategory category = IssueCategory.StateConsistency,
        IReadOnlyList<string>? repairTargetFiles = null) =>
        new(
            path,
            severity,
            "Unified resource contract rejected the submitted semantic.",
            code: code,
            actor: actor,
            section: section,
            expected: "one exact GM-authored semantic",
            actual: actual,
            category: category,
            repairTargetFiles: repairTargetFiles);
}
