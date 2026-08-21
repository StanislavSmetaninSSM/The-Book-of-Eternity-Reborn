using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMaterializationValidationTests
{
    [Fact]
    public async Task WoundConsequence_ApplyPublishesNoWoundWriteCapability()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreateSourceBoundWoundConsequenceDefinition();
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var command = CreateWoundConsequenceApplyCommand();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
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
        var effect = await ReadSinglePlayerEffectAsync(context);
        Assert.Equal(
            "wound_consequence",
            effect["components"]![0]!["profile"]!.GetValue<string>());
        Assert.Equal(
            "wound_test_torn_side",
            effect["links"]![0]!["targetId"]!.GetValue<string>());
        Assert.Equal("source_bound", effect["lifetime"]!["mode"]!.GetValue<string>());
        Assert.Equal("wound", effect["lifetime"]!["linkKind"]!.GetValue<string>());
        Assert.Equal(
            "wound_test_torn_side",
            effect["lifetime"]!["targetId"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("dispel", "physical_treatment", "dispelled")]
    [InlineData("remove", "stop_bleeding", "removed")]
    public async Task WoundConsequence_TerminalEffectOperationNeverTreatsWound(
        string operation,
        string authorityKind,
        string terminalState)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreateSourceBoundWoundConsequenceDefinition();
        var existing = CreateSourceBoundWoundConsequenceEffect(definition);
        await SeedLifecycleStateAsync(context, definition, existing);
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
    public async Task WoundConsequence_DueExpiryLeavesWoundByteExact()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "wound_consequence");
        var existing = CreateLifecycleEffect(
            definition,
            currentStacks: 1,
            remainingTurns: 1);
        await SeedLifecycleStateAsync(context, definition, existing);
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
    public async Task WoundTreatment_ExpiresDeclaredSourceBoundConsequenceWithoutRewritingTreatment()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreateSourceBoundWoundConsequenceDefinition();
        var existing = CreateSourceBoundWoundConsequenceEffect(definition);
        await SeedLifecycleStateAsync(context, definition, existing);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var wounds = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath))!.AsArray();
        wounds[0]!["isHealed"] = true;
        wounds[0]!["healingState"] = new JsonObject
        {
            ["state"] = "healed",
            ["treatedAtTurn"] = 43,
            ["treatment"] = "surgical_closure"
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            wounds);
        var acceptedTreatment = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerWoundsPath);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        AssertNoEffectErrors(issues);
        var plan = await context.NormalizeAcceptedEffectsAsync(backups);
        Assert.NotNull(plan);
        AssertEffectPlanCannotWriteWounds(plan);
        Assert.Equal(
            acceptedTreatment,
            await context.CaptureBytesAsync(
                EffectMaterializationTestContext.PlayerWoundsPath));
        var player = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Empty(player["activeEffects"]!.AsArray());
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = index["entries"]![0]!.AsObject();
        Assert.Equal("expired", entry["state"]!.GetValue<string>());
        var transition = entry["transitions"]!.AsArray()[^1]!.AsObject();
        Assert.Equal("expire", transition["kind"]!.GetValue<string>());
    }

    [Fact]
    public async Task EffectCommand_CannotCarryDirectWoundMutation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreateSourceBoundWoundConsequenceDefinition();
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateWoundConsequenceApplyCommand();
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
    public async Task WoundConsequence_LateSourceTreatmentRollsBackEffectPublicationOnly()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreateSourceBoundWoundConsequenceDefinition();
        var existing = CreateSourceBoundWoundConsequenceEffect(definition);
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
        var wounds = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath))!.AsArray();
        wounds[0]!["isHealed"] = true;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            wounds);
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        Assert.Equal(
            before,
            await context.CaptureBytesAsync(
                EffectMaterializationTestContext.PlayerWoundsPath,
                EffectMaterializationTestContext.PlayerEffectsPath,
                EffectMaterializationTestContext.IdentityIndexPath,
                EffectMaterializationTestContext.CommandPath));
    }

    [Fact]
    public async Task WoundConsequence_NpcCarrierPreservesAdjacentNpcWoundState()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreateSourceBoundWoundConsequenceDefinition();
        await context.SeedPlayerWoundSourceAsync(definition);
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
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var command = CreateWoundConsequenceApplyCommand("npc");
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        AssertNoEffectErrors(issues);
        var plan = await context.NormalizeAcceptedEffectsAsync(backups);
        Assert.NotNull(plan);
        AssertEffectPlanCannotWriteWounds(plan);
        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath))!.AsObject();
        Assert.True(JsonNode.DeepEquals(adjacentWounds, root["NPCWoundChanges"]));
        Assert.Equal(
            "2026-08-22T00:00:00Z",
            root["_lastUpdated"]!.GetValue<string>());
        var entry = Assert.IsType<JsonObject>(Assert.Single(root["entries"]!.AsArray()));
        Assert.Single(entry["activeEffects"]!.AsArray());
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

    private static JsonObject CreateSourceBoundWoundConsequenceEffect(
        JsonObject definition)
    {
        var effect = CreateLifecycleEffect(definition, currentStacks: 1);
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["linkKind"] = "wound",
            ["targetId"] = "wound_test_torn_side",
            ["activePredicate"] = "active",
            ["onSourceLoss"] = "expire"
        };
        return effect;
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
