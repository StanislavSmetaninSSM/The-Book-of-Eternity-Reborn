using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMaterializationValidationTests
{
    [Fact]
    public async Task WoundConsequence_OrdinaryApplyIsRejectedWithoutWoundWriteCapability()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreateSourceBoundWoundConsequenceDefinition();
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateWoundConsequenceApplyCommand();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
        var woundBefore = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerWoundsPath);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_selector_unresolved");
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
        Assert.Equal(
            woundBefore,
            await context.CaptureBytesAsync(
                EffectMaterializationTestContext.PlayerWoundsPath));
        Assert.Null(await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath));
        Assert.Null(await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath));
    }

    [Theory]
    [InlineData("dispel", "physical_treatment", "dispelled")]
    [InlineData("remove", "stop_bleeding", "removed")]
    public async Task TerminalEffectOperation_NeverMutatesCanonicalWoundCarrier(
        string operation,
        string authorityKind,
        string terminalState)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await SeedCanonicalEmptyPlayerWoundCarrierAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateTerminalCommand(operation, authorityKind, "turn_43")));
        var woundBefore = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerWoundsPath);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        AssertNoEffectErrors(issues);
        var plan = await context.NormalizeAcceptedEffectsAsync(backups);
        Assert.NotNull(plan);
        AssertEffectPlanCannotWriteWounds(plan);
        Assert.Equal(
            woundBefore,
            await context.CaptureBytesAsync(
                EffectMaterializationTestContext.PlayerWoundsPath));
        var player = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Empty(player["activeEffects"]!.AsArray());
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        Assert.Equal(
            terminalState,
            index["entries"]![0]!["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task DueEffectExpiry_LeavesCanonicalWoundCarrierByteExact()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var existing = CreateLifecycleEffect(
            definition,
            currentStacks: 1,
            remainingTurns: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await SeedCanonicalEmptyPlayerWoundCarrierAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var woundBefore = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerWoundsPath);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        AssertNoEffectErrors(issues);
        var plan = await context.NormalizeAcceptedEffectsAsync(backups);
        Assert.NotNull(plan);
        AssertEffectPlanCannotWriteWounds(plan);
        Assert.Equal(
            woundBefore,
            await context.CaptureBytesAsync(
                EffectMaterializationTestContext.PlayerWoundsPath));
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        Assert.Equal("expired", index["entries"]![0]!["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task DirectLegacyWoundTreatmentMutation_FailsClosedWithoutEffectPublication()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedCanonicalEmptyPlayerWoundCarrierAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            new JsonArray(new JsonObject
            {
                ["woundId"] = "wound_test_torn_side",
                ["isHealed"] = true,
                ["healingState"] = new JsonObject
                {
                    ["state"] = "healed",
                    ["treatedAtTurn"] = 43,
                    ["treatment"] = "surgical_closure"
                }
            }));
        var acceptedMutation = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerWoundsPath);

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalWoundMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.FilePath.StartsWith(
                EffectMaterializationTestContext.PlayerWoundsPath,
                StringComparison.Ordinal));
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
        Assert.Equal(
            acceptedMutation,
            await context.CaptureBytesAsync(
                EffectMaterializationTestContext.PlayerWoundsPath));
        Assert.Null(await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath));
        Assert.Null(await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath));
    }

    [Fact]
    public async Task EffectCommand_CannotCarryDirectWoundMutation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerSkillSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["woundChanges"] = new JsonArray(new JsonObject
        {
            ["woundId"] = "wound_test_torn_side",
            ["isHealed"] = true
        });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_plan_unknown_field" &&
            issue.FilePath.EndsWith(".woundChanges", StringComparison.Ordinal));
        Assert.Equal(
            before,
            await context.CaptureBytesAsync(
                EffectMaterializationTestContext.PlayerWoundsPath,
                EffectMaterializationTestContext.PlayerEffectsPath,
                EffectMaterializationTestContext.IdentityIndexPath));
    }

    [Fact]
    public async Task LateEffectCommandMutation_RollsBackEffectPublicationOnly()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        var existing = CreateLifecycleEffect(definition, currentStacks: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateTerminalCommand("remove", "stop_bleeding", "turn_43")));
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        AssertNoEffectErrors(issues);
        var command = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.CommandPath))!.AsObject();
        command["effectChanges"]![0]!["reason"] =
            "Команда изменена после запечатывания плана.";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            command);
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        Assert.Equal(
            before,
            await context.CaptureBytesAsync(
                EffectMaterializationTestContext.PlayerEffectsPath,
                EffectMaterializationTestContext.IdentityIndexPath,
                EffectMaterializationTestContext.CommandPath));
    }

    [Fact]
    public async Task EffectApply_LegacyNpcWoundPayloadIsRejectedBeforePublication()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "action_control");
        await context.SeedPlayerSkillSourceAsync(definition);
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_test_healer"
                })
            });
        var adjacentWounds = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_test_healer",
            ["wounds"] = new JsonArray(new JsonObject
            {
                ["woundId"] = "wound_npc_arm",
                ["severity"] = "moderate",
                ["healingState"] = new JsonObject
                {
                    ["state"] = "stabilized"
                }
            })
        });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath,
            new JsonObject
            {
                ["NPCWoundChanges"] = adjacentWounds.DeepClone(),
                ["_lastUpdated"] = "2026-08-22T00:00:00Z"
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand("npc");
        command["parameters"] = new JsonObject();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "accepted_mechanics_effect_owner_delta_invalid");
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath))!.AsObject();
        Assert.True(JsonNode.DeepEquals(adjacentWounds, root["NPCWoundChanges"]));
        Assert.Equal(
            "2026-08-22T00:00:00Z",
            root["_lastUpdated"]!.GetValue<string>());
        Assert.False(root.ContainsKey("entries"));
    }

    private static JsonObject CreateSourceBoundWoundConsequenceDefinition()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "wound_consequence");
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "active",
            ["onSourceLoss"] = "expire"
        };
        return definition;
    }

    private static Task SeedCanonicalEmptyPlayerWoundCarrierAsync(
        EffectMaterializationTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["owner"] = new JsonObject
                {
                    ["realm"] = "mortal_world",
                    ["ownerKind"] = "player",
                    ["ownerId"] = "player_current",
                },
                ["activeWounds"] = new JsonArray()
            });
    }

    private static JsonObject CreateWoundConsequenceApplyCommand(
        string targetKind = "player")
    {
        var command = EffectMaterializationTestFixture.CreateApplyCommand(targetKind);
        command["parameters"] = new JsonObject();
        return command;
    }

    private static void AssertEffectPlanCannotWriteWounds(
        EffectAcceptedTurnPlan plan)
    {
        Assert.DoesNotContain(
            EffectMaterializationTestContext.PlayerWoundsPath,
            plan.CarrierAfterImages.Keys);
        Assert.DoesNotContain(
            EffectMaterializationTestContext.PlayerWoundsPath,
            plan.CarrierBeforeImages.Keys);
        Assert.DoesNotContain(
            EffectMaterializationTestContext.PlayerWoundsPath,
            plan.TouchedPaths);
        Assert.DoesNotContain(
            EffectMaterializationTestContext.PlayerWoundsPath,
            plan.DeletedPaths);
    }
}
