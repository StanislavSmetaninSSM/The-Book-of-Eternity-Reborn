using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceFullPartyInteractionTests
{
    [Fact]
    public async Task AcceptedRemotePacket_IsStagedWithoutLocalMechanicsMutation()
    {
        await using var context = await CreateContextAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        var accepted = FullPartyRoot(("player_remote", Packet(
            RemoteChange("turn_42:resource:1"))));
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.FullPartyInteractionsPath,
            accepted.ToJsonString());
        var interactionBytes = await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationTestContext.FullPartyInteractionsPath);
        var stateBefore = await context.ReadJsonAsync(
            ResourceMaterializationTestContext.StatePath);
        var historyBefore = await context.ReadJsonAsync(
            ResourceMaterializationTestContext.HistoryPath);
        var effectIndexBefore = await context.ReadJsonAsync(
            EffectAcceptedTurnPlan.IdentityIndexPath);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out var binding,
            out var planning));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planning.Plan);
        Assert.Empty(plan.ResourceEvents);
        Assert.Single(binding.AcceptedEvents["events"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(stateBefore, plan.StateAfterImage));
        Assert.True(JsonNode.DeepEquals(historyBefore, plan.HistoryAfterImage));
        Assert.Contains(
            ResourceMaterializationTestContext.FullPartyInteractionsPath,
            plan.BeforeImages.Keys);

        await using (var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var published = await context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups: null);
            Assert.Same(plan, published);
        }

        Assert.True(JsonNode.DeepEquals(
            stateBefore,
            await context.ReadJsonAsync(ResourceMaterializationTestContext.StatePath)));
        Assert.True(JsonNode.DeepEquals(
            historyBefore,
            await context.ReadJsonAsync(ResourceMaterializationTestContext.HistoryPath)));
        Assert.True(JsonNode.DeepEquals(
            effectIndexBefore,
            await context.ReadJsonAsync(EffectAcceptedTurnPlan.IdentityIndexPath)));
        Assert.Equal(
            interactionBytes,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationTestContext.FullPartyInteractionsPath));
    }

    [Fact]
    public async Task AcceptedRemotePackets_ShareGlobalOrdinalAfterLocalCommands()
    {
        await using var context = await CreateContextAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.FullPartyInteractionsPath,
            FullPartyRoot(
                ("player_zeta", Packet(RemoteChange("turn_42:resource:3"))),
                ("player_alpha", Packet(RemoteChange("turn_42:resource:2"))))
                .ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out var binding,
            out _));
        Assert.Equal(
            new[]
            {
                "turn_42:resource:1",
                "turn_42:resource:2",
                "turn_42:resource:3"
            },
            binding.AcceptedEvents["events"]!.AsArray()
                .Select(static value => value!["eventRef"]!.GetValue<string>()));
    }

    [Fact]
    public async Task UnchangedHistoricalPacket_IsNotReplayedAsCurrentTurnAuthority()
    {
        await using var context = await CreateContextAsync();
        var historical = FullPartyRoot(("player_remote", Packet(
            RemoteChange("turn_41:resource:1"))));
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.FullPartyInteractionsPath,
            historical.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
        Assert.True(JsonNode.DeepEquals(
            historical,
            await context.ReadJsonAsync(
                ResourceMaterializationTestContext.FullPartyInteractionsPath)));
    }

    [Fact]
    public async Task MetaStateValidation_UsesTheClosedFullPartyResourcePacketContract()
    {
        await using var context = await CreateContextAsync();
        var packet = Packet(RemoteChange("turn_42:resource:1"));
        packet["resourceCapacityChanges"] = new JsonArray();
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.FullPartyInteractionsPath,
            FullPartyRoot(("player_remote", packet)).ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(
                GameStateValidationPhase.MetaMiscStateFiles,
                new[] { ResourceMaterializationTestContext.FullPartyInteractionsPath }));

        Assert.Contains(issues, issue =>
            issue.Code == "resource_full_party_packet_unknown_field");
    }

    [Fact]
    public async Task MetaStateValidation_RejectsLegacyArrayFileRoot()
    {
        await using var context = await CreateContextAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.FullPartyInteractionsPath,
            "[]");

        var issues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(
                GameStateValidationPhase.MetaMiscStateFiles,
                new[] { ResourceMaterializationTestContext.FullPartyInteractionsPath }));

        Assert.Contains(issues, issue => issue.Code == "strict_state_invalid_root");
    }

    [Theory]
    [InlineData("npc", "npc_remote", false, "combat_outcome")]
    [InlineData("player", "player_remote", false, "narrative_outcome")]
    [InlineData("player", "player_current", true, "action_cost")]
    [InlineData("player", "player_current", false, "local_item_cost")]
    public async Task RawValidation_RemotePacketCannotEscapeRecipientLocalPlayerScope(
        string targetKind,
        string targetIdentity,
        bool useTargetRef,
        string sourceKind)
    {
        await using var context = await CreateContextAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        var command = RemoteChange("turn_42:resource:1");
        command["target"] = new JsonObject
        {
            ["kind"] = targetKind,
            [useTargetRef ? "targetRef" : "targetId"] = targetIdentity
        };
        command["source"] = sourceKind == "local_item_cost"
            ? new JsonObject
            {
                ["kind"] = sourceKind,
                ["sourceId"] = "item_remote"
            }
            : new JsonObject { ["kind"] = sourceKind };
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.FullPartyInteractionsPath,
            FullPartyRoot(("player_remote", Packet(command))).ToJsonString());
        var before = await context.CaptureAsync(ResourceMaterializationTestContext.AllResourcePaths);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code is "resource_full_party_target_scope_invalid" or
                "resource_full_party_source_scope_invalid");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
        await context.AssertUnchangedAsync(before);
    }

    [Theory]
    [InlineData("array_root")]
    [InlineData("active_player_recipient")]
    [InlineData("confusable_recipient")]
    [InlineData("empty_bucket")]
    [InlineData("unknown_packet_field")]
    [InlineData("duplicate_property")]
    public async Task RawValidation_MalformedRemoteEnvelopeFailsClosed(string mutation)
    {
        await using var context = await CreateContextAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        var json = mutation switch
        {
            "array_root" => "{\"otherPlayersInteractions\":[]}",
            "active_player_recipient" => FullPartyRoot(
                ("player_current", Packet(RemoteChange("turn_42:resource:1"))))
                .ToJsonString(),
            "confusable_recipient" => FullPartyRoot(
                ("player_alpha", Packet(RemoteChange("turn_42:resource:1"))),
                ("PLAYER_ALPHA", Packet(RemoteChange("turn_42:resource:2"))))
                .ToJsonString(),
            "empty_bucket" => "{\"otherPlayersInteractions\":{\"player_remote\":[]}}",
            "unknown_packet_field" =>
                "{\"otherPlayersInteractions\":{\"player_remote\":[{" +
                "\"resourceChanges\":[],\"resourceCapacityChanges\":[]}]}}",
            "duplicate_property" =>
                "{\"otherPlayersInteractions\":{\"player_remote\":[{" +
                "\"resourceChanges\":[{" +
                "\"operation\":\"spend\",\"operation\":\"gain\"," +
                "\"target\":{\"kind\":\"player\",\"targetId\":\"player_current\"}," +
                "\"resourceKey\":\"energy\",\"amount\":1," +
                "\"source\":{\"kind\":\"action_cost\"}," +
                "\"eventRef\":\"turn_42:resource:1\",\"reason\":\"duplicate\"}]}]}}",
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.FullPartyInteractionsPath,
            json);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.FilePath.Contains("otherPlayersInteractions", StringComparison.Ordinal));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
    }

    [Theory]
    [InlineData("reused")]
    [InlineData("swapped")]
    public async Task RawValidation_RemoteReplayOrSwappedOrdinalFailsClosed(string mutation)
    {
        await using var context = await CreateContextAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        var first = mutation == "swapped"
            ? "turn_42:resource:2"
            : "turn_42:resource:1";
        var second = mutation == "swapped"
            ? "turn_42:resource:1"
            : "turn_42:resource:1";
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.FullPartyInteractionsPath,
            FullPartyRoot(("player_remote", Packet(
                RemoteChange(first),
                RemoteChange(second)))).ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "resource_command_event_authority_mismatch");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
    }

    [Fact]
    public async Task Publication_LateRemotePacketMutationFailsBeforeAnyMechanicsWrite()
    {
        await using var context = await CreateContextAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        var accepted = FullPartyRoot(("player_remote", Packet(
            RemoteChange("turn_42:resource:1"))));
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.FullPartyInteractionsPath,
            accepted.ToJsonString());
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var mechanicsBefore = await context.CaptureAsync(
            ResourceMaterializationTestContext.DefinitionsPath,
            ResourceMaterializationTestContext.StatePath,
            ResourceMaterializationTestContext.HistoryPath,
            EffectAcceptedTurnPlan.IdentityIndexPath);
        accepted["otherPlayersInteractions"]!["player_remote"]![0]![
            "resourceChanges"]![0]!["reason"] = "Late semantic mutation";
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.FullPartyInteractionsPath,
            accepted.ToJsonString());

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups: null));

        await context.AssertUnchangedAsync(mechanicsBefore);
    }

    private static async Task<ResourceMaterializationTestContext> CreateContextAsync()
    {
        var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.WriteExactJsonAsync(
            EffectAcceptedTurnPlan.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex().ToJsonString());
        return context;
    }

    private static JsonObject FullPartyRoot(
        params (string RecipientId, JsonObject Packet)[] recipients)
    {
        var buckets = new JsonObject();
        foreach (var (recipientId, packet) in recipients)
            buckets[recipientId] = new JsonArray(packet.DeepClone());
        return new JsonObject
        {
            ["otherPlayersInteractions"] = buckets,
            ["_lastUpdated"] = "2026-08-22T00:00:00Z"
        };
    }

    private static JsonObject Packet(params JsonObject[] changes) => new()
    {
        ["resourceChanges"] = new JsonArray(
            changes.Select(static value => (JsonNode)value.DeepClone()).ToArray())
    };

    private static JsonObject RemoteChange(string eventRef) => new()
    {
        ["operation"] = "spend",
        ["target"] = new JsonObject
        {
            ["kind"] = "player",
            ["targetId"] = "player_current"
        },
        ["resourceKey"] = "energy",
        ["amount"] = 1,
        ["source"] = new JsonObject { ["kind"] = "action_cost" },
        ["eventRef"] = eventRef,
        ["reason"] = "Remote accepted action cost"
    };
}
