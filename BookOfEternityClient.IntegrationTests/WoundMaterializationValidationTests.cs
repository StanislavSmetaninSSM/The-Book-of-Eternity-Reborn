using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundMaterializationValidationTests
{
    private static readonly string[] SnapshotWoundPaths =
    {
        WoundCarrierCatalog.PlayerPath,
        WoundCarrierCatalog.NpcPath,
        WoundIdentityState.StatePath,
        WoundHistoryState.HistoryPath
    };

    [Fact]
    public async Task EmptyStrictCommand_ComposesOneSealedWoundStageIntoCommonPlan()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        var snapshotToken = await ReadSnapshotTokenAsync(context);
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            EmptyCommands(
                sessionId: "session_resource_materialization",
                requestId: "request_resource_materialization",
                snapshotToken: snapshotToken).ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);

        var errors = issues
            .Where(static issue => issue.Severity == IssueSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            string.Join(
                Environment.NewLine,
                errors.Select(static issue => issue.ToString())));
        Assert.True(
            WoundAcceptedTurnPlanAuthority.TryPeekPrepared(
                context.FileSystem,
                lease,
                out _),
            "The raw wound command did not reach the prepared wound stage.");
        Assert.True(
            AcceptedMechanicsPlanAuthority.TryPeekValidated(
                context.FileSystem,
                lease,
                out var binding,
                out var result),
            string.Join(
                Environment.NewLine,
                issues.Select(static issue => issue.ToString())));
        Assert.True(result.Success);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
        var stages = Assert.IsType<AcceptedMechanicsWoundStageBundle>(
            plan.WoundStageBundle);
        Assert.Empty(stages.Input.Transitions);
        Assert.Empty(stages.PreparedPlan.EffectOperationBatches);
        Assert.Equal(snapshotToken, stages.Input.Binding.SnapshotToken);
        Assert.NotNull(binding.WoundCommands);
        Assert.NotNull(binding.WoundInput);
        Assert.Contains(AcceptedMechanicsPlan.WoundCommandPath, plan.ConsumedPaths);
    }

    [Fact]
    public async Task StrictCommand_RejectsUnknownRootFieldBeforeAnyCommonHandoff()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        var command = EmptyCommands(
            sessionId: "session_resource_materialization",
            requestId: "request_resource_materialization",
            snapshotToken: await ReadSnapshotTokenAsync(context));
        command["gmOwnedShortcut"] = true;
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            command.ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);

        Assert.Contains(issues, issue =>
            issue.FilePath == AcceptedMechanicsPlan.WoundCommandPath + ".gmOwnedShortcut" &&
            issue.Code == "wound_command_unknown_field");
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            lease,
            out _,
            out _));
    }

    [Theory]
    [InlineData("duplicate_schema", "wound_command_duplicate_field")]
    [InlineData("missing_request", "wound_command_missing_field")]
    [InlineData("wrong_snapshot", "wound_command_binding_mismatch")]
    [InlineData("unadapted_command", "wound_command_transition_adapter_unavailable")]
    public async Task StrictCommand_RejectsMalformedOrUnadaptedEnvelopeBeforePlanning(
        string mutation,
        string expectedCode)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        var command = EmptyCommands(
            sessionId: "session_resource_materialization",
            requestId: "request_resource_materialization",
            snapshotToken: await ReadSnapshotTokenAsync(context));
        var commandJson = mutation switch
        {
            "duplicate_schema" => command.ToJsonString().Replace(
                "\"schemaVersion\":1",
                "\"schemaVersion\":1,\"schemaVersion\":1",
                StringComparison.Ordinal),
            "missing_request" => RemoveCommandField(command, "requestId"),
            "wrong_snapshot" => SetCommandField(
                command,
                "snapshotToken",
                "snapshot_wrong"),
            "unadapted_command" => AddUnadaptedCommand(command),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            commandJson);

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);

        Assert.Contains(issues, issue => issue.Code == expectedCode);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            lease,
            out _,
            out _));
    }

    [Fact]
    public async Task RawValidation_RejectsChangedClientOwnedIdentityAgainstSnapshot()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyFoundationsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: SnapshotWoundPaths);
        var snapshotToken = await ReadSnapshotTokenAsync(context);
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            EmptyCommands(
                sessionId: "session_resource_materialization",
                requestId: "request_resource_materialization",
                snapshotToken: snapshotToken).ToJsonString());
        var changedIdentity = EmptyIdentity();
        changedIdentity["entries"]!.AsArray().Add(
            new JsonObject
            {
                ["woundId"] = "wound_test_torn_side",
                ["realm"] = "mortal_world",
                ["ownerKind"] = "player",
                ["ownerId"] = "player_current",
                ["carrierPath"] = WoundCarrierCatalog.PlayerPath,
                ["domain"] = "physical",
                ["status"] = "active",
                ["createdAtTurn"] = 42,
                ["createdEventRef"] = "turn_42:wound_opened",
                ["lastTransitionOrdinal"] = 1,
                ["terminalTransitionId"] = null,
                ["semanticFingerprint"] =
                    "sha256:0000000000000000000000000000000000000000000000000000000000000000"
            });
        await context.WriteExactJsonAsync(
            WoundIdentityState.StatePath,
            changedIdentity.ToJsonString());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);

        Assert.Contains(issues, issue =>
            issue.FilePath == WoundIdentityState.StatePath &&
            issue.Code == "wound_materialization_client_owned_root_mutated" &&
            issue.Category == IssueCategory.ClientOwnedSurface);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            lease,
            out _,
            out _));
    }

    private static async Task SeedEmptyFoundationsAsync(
        ResourceMaterializationTestContext context)
    {
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.WriteExactJsonAsync(
            WoundCarrierCatalog.PlayerPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["owner"] = new JsonObject
                {
                    ["realm"] = "mortal_world",
                    ["ownerKind"] = "player",
                    ["ownerId"] = "player_current"
                },
                ["activeWounds"] = new JsonArray()
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            WoundCarrierCatalog.NpcPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            WoundIdentityState.StatePath,
            EmptyIdentity().ToJsonString());
        await context.WriteExactJsonAsync(
            WoundHistoryState.HistoryPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["nextOrdinal"] = 1,
                ["transitions"] = new JsonArray()
            }.ToJsonString());
    }

    private static JsonObject EmptyIdentity() => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray()
    };

    private static JsonObject EmptyCommands(
        string sessionId,
        string requestId,
        string snapshotToken) => new()
    {
        ["schemaVersion"] = 1,
        ["sessionId"] = sessionId,
        ["requestId"] = requestId,
        ["snapshotToken"] = snapshotToken,
        ["commands"] = new JsonArray()
    };

    private static string RemoveCommandField(JsonObject command, string field)
    {
        Assert.True(command.Remove(field));
        return command.ToJsonString();
    }

    private static string SetCommandField(
        JsonObject command,
        string field,
        JsonNode value)
    {
        command[field] = value;
        return command.ToJsonString();
    }

    private static string AddUnadaptedCommand(JsonObject command)
    {
        command["commands"]!.AsArray().Add(new JsonObject());
        return command.ToJsonString();
    }

    private static async Task<string> ReadSnapshotTokenAsync(
        ResourceMaterializationTestContext context)
    {
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"));
        return manifest["manifestPayloadHash"]!.GetValue<string>();
    }
}
