using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class CanonicalStateNormalizerEffectTests
{
    [Fact]
    public void EffectAuthorityPathsAreTrackedForSnapshotPublicationAndRollback()
    {
        foreach (var path in new[]
                 {
                     EffectCarrierCatalog.PlayerPath,
                     EffectCarrierCatalog.NpcPath,
                     EffectCarrierCatalog.EnemiesPath,
                     EffectCarrierCatalog.AlliesPath,
                     EffectAcceptedTurnPlan.IdentityIndexPath
                 })
        {
            Assert.Contains(path, CanonicalStateNormalizer.CanonicalAccumulatedFiles);
            Assert.Contains(path, CanonicalStateNormalizer.NormalizerBackupInputFiles);
            Assert.Contains(path, CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        }

        foreach (var path in EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
            Assert.Contains(path, CanonicalStateNormalizer.NormalizerBackupInputFiles);
        Assert.Contains(
            EffectAcceptedTurnPlan.CommandPath,
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
    }

    [Fact]
    public async Task AcceptedMechanics_WithoutCommandsOrCombatantRefsDoesNotRequireTurnRequest()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();

        var plan = await context.NormalizeAcceptedEffectsAsync(backups: null);

        Assert.Null(plan);
    }

    [Fact]
    public async Task Apply_PlayerWritesCompleteCarrierAndIndexThenConsumesCommand()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));

        await ValidateRawPlanAsync(context);
        await context.NormalizeAcceptedEffectsAsync(backups);

        await AssertCanonicalCarrierPassesLegacyStatePhaseAsync(
            context,
            GameStateValidationPhase.PlayerStateFiles,
            EffectMaterializationTestContext.PlayerEffectsPath,
            $"{EffectMaterializationTestContext.PlayerEffectsPath}.activeEffects");

        var player = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        var effect = Assert.IsType<JsonObject>(Assert.Single(player["activeEffects"]!.AsArray()));
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()));
        Assert.Equal(effect["effectId"]!.GetValue<string>(), entry["effectId"]!.GetValue<string>());
        var transitions = entry["transitions"]!.AsArray();
        Assert.Equal(
            effect["chronology"]!["lastTransitionId"]!.GetValue<string>(),
            transitions[transitions.Count - 1]!["transitionId"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(EffectMaterializationTestContext.CommandPath));

        var wound = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath))!.AsArray()[0]!.AsObject();
        Assert.Equal("severe", wound["severity"]!.GetValue<string>());
        Assert.Single(wound["activeEffectDefinitions"]!.AsArray());
    }

    [Fact]
    public async Task AcceptedMechanics_AfterSuccessfulPublicationConsumesValidatedPlan()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));

        await ValidateRawPlanAsync(context);
        var publishedPlan = await context.NormalizeAcceptedEffectsAsync(backups);
        Assert.NotNull(publishedPlan);

        context.FileSystem.DeleteFile("input/turn_request.json");
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var repeatedPlan = await context.NormalizeAcceptedEffectsAsync(backups: null);

        Assert.Null(repeatedPlan);
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Apply_WithoutValidatedAcceptedPlanFailsClosedBeforePublication()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        Assert.Contains("validated common plan", exception.Message, StringComparison.Ordinal);
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("carrier")]
    [InlineData("identity_index")]
    [InlineData("source")]
    [InlineData("target")]
    public async Task Apply_ValidatedPlanInvalidatesWhenAuthorityChangesBeforePublication(
        string changedAuthority)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync(CreateNonResourceDefinition());
        var targetKind = changedAuthority == "target" ? "npc" : "player";
        if (targetKind == "npc")
        {
            await context.WriteJsonAsync(
                "game_state/npcs/npc_core.json",
                new JsonObject
                {
                    ["NPCsInScene"] = new JsonArray(new JsonObject
                    {
                        ["NPCId"] = "npc_test_healer"
                    })
                });
        }
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateNonResourceApplyCommand(targetKind)));
        await ValidateRawPlanAsync(context);

        switch (changedAuthority)
        {
            case "carrier":
                await context.WriteJsonAsync(
                    EffectMaterializationTestContext.PlayerEffectsPath,
                    new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["activeEffects"] = new JsonArray(),
                        ["lateMutation"] = true
                    });
                break;
            case "identity_index":
                await context.WriteJsonAsync(
                    EffectMaterializationTestContext.IdentityIndexPath,
                    new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["entries"] = new JsonArray()
                    });
                break;
            case "source":
                var wounds = (await context.ReadJsonAsync(
                    EffectMaterializationTestContext.PlayerWoundsPath))!.AsArray();
                wounds[0]!["activeEffectDefinitions"]![0]!["display"]!["title"] =
                    "Поздно изменённое определение";
                await context.WriteJsonAsync(
                    EffectMaterializationTestContext.PlayerWoundsPath,
                    wounds);
                break;
            case "target":
                await context.WriteJsonAsync(
                    "game_state/npcs/npc_core.json",
                    new JsonObject { ["NPCsInScene"] = new JsonArray() });
                break;
            default:
                throw new InvalidOperationException(changedAuthority);
        }

        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.NpcEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        Assert.Contains("changed after", exception.Message, StringComparison.Ordinal);
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.NpcEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Apply_ValidatedPlanRejectsUntouchedCarrierIndexDivergenceBeforePublication()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync(CreateNonResourceDefinition());
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_test_healer"
                })
            });
        var existingEffect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            "npc",
            "action_control");
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_test_healer",
                    ["activeEffects"] = new JsonArray(existingEffect.DeepClone())
                })
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(existingEffect));
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateNonResourceApplyCommand()));
        await ValidateRawPlanAsync(context);

        await context.WriteJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_test_healer",
                    ["activeEffects"] = new JsonArray()
                })
            });
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.NpcEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        Assert.Contains("changed after validation", exception.Message, StringComparison.Ordinal);
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.NpcEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Apply_ValidatedPlanRejectsCoordinatedUntouchedEffectAndSourceMutation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync(CreateNonResourceDefinition());
        var wounds = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath))!.AsArray();
        var secondWound = wounds[0]!.DeepClone().AsObject();
        secondWound["woundId"] = "wound_test_second_source";
        var secondDefinition = secondWound["activeEffectDefinitions"]![0]!.AsObject();
        secondDefinition["definitionKey"] = "second_bleeding_consequence";
        wounds.Add(secondWound);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            wounds);
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_test_healer"
                })
            });
        var existingEffect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            "npc",
            "action_control");
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_test_healer",
                    ["activeEffects"] = new JsonArray(existingEffect.DeepClone())
                })
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(existingEffect));
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var command = CreateNonResourceApplyCommand();
        command["source"]!["sourceId"] = "wound_test_second_source";
        command["source"]!["definitionKey"] = "second_bleeding_consequence";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
        await ValidateRawPlanAsync(context);

        var npcRoot = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath))!.AsObject();
        npcRoot["entries"]![0]!["activeEffects"]![0]!["components"]![0]!["payload"]!["action"] = "attack";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath,
            npcRoot);
        wounds = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath))!.AsArray();
        wounds[0]!["activeEffectDefinitions"]![0]!["components"]![0]!["payload"]!["action"] = "attack";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            wounds);
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.NpcEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestContext.PlayerWoundsPath);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        Assert.Contains("changed after validation", exception.Message, StringComparison.Ordinal);
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.NpcEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestContext.PlayerWoundsPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Apply_ValidatedPlanRejectsUntouchedEmptyCombatantIdentityMutation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        var combatant = EffectMaterializationTestFixture.CreateSameTurnCombatant(
            "combatant_ref_preexisting_empty");
        combatant.Remove("combatantRef");
        combatant["combatantId"] = "combatant_preexisting_empty";
        combatant.Remove("currentHealth");
        combatant.Remove("maxHealth");
        combatant.Remove("currentPoise");
        combatant.Remove("maxPoise");
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray(combatant) });
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));
        await ValidateRawPlanAsync(context);

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        root["enemiesData"]![0]!["combatantId"] = "combatant_late_forged_identity";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            root);
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.EnemyCombatantsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        Assert.Contains("changed after validation", exception.Message, StringComparison.Ordinal);
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.EnemyCombatantsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Apply_FailedRawRevalidationInvalidatesPreviouslyValidatedPlan()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(new JsonObject
                {
                    ["eventId"] = "event_cache_guard",
                    ["summary"] = "Событие хранит неприменяемые вложенные данные.",
                    ["isActive"] = true,
                    ["payload"] = new JsonObject()
                })
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));
        await ValidateRawPlanAsync(context);

        var worldEvents = (await context.ReadJsonAsync(
            "game_state/world/world_events.json"))!.AsObject();
        var malformedDefinition = EffectMaterializationTestFixture.CreateDefinition();
        malformedDefinition.Remove("removal");
        worldEvents["worldEventsLog"]![0]!["payload"]!["activeEffectDefinitions"] =
            new JsonArray(malformedDefinition);
        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            worldEvents);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_missing_field");
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        Assert.Contains("requires one validated common plan", exception.Message, StringComparison.Ordinal);
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Apply_ValidatedPlanRejectsLateDuplicateCommandProperty()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var commands = EffectMaterializationTestFixture.CreateCommandRoot(
            EffectMaterializationTestFixture.CreateApplyCommand());
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            commands);
        await ValidateRawPlanAsync(context);

        var validJson = commands.ToJsonString();
        var duplicateJson = validJson[..^1] + ",\"effectResolutionReceipts\":[]}";
        await context.FileSystem.WriteFileAtomicAsync(
            EffectMaterializationTestContext.CommandPath,
            duplicateJson);
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        Assert.Contains("changed after validation", exception.Message, StringComparison.Ordinal);
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Apply_ValidatedPlanRejectsLateAcceptedTurnMutation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));
        await ValidateRawPlanAsync(context);

        var request = (await context.ReadJsonAsync("input/turn_request.json"))!.AsObject();
        request["turnNumber"] = 43;
        await context.WriteJsonAsync("input/turn_request.json", request);
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Apply_ReceiptsOnlyRootIsValidatedAndConsumedAsNoOp()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            new JsonObject
            {
                ["effectResolutionReceipts"] = new JsonArray()
            });

        await ValidateRawPlanAsync(context);
        var plan = await context.NormalizeAcceptedEffectsAsync(backups);

        Assert.NotNull(plan);
        Assert.Empty(plan.ActiveEffects);
        Assert.False(context.FileSystem.FileExists(EffectMaterializationTestContext.CommandPath));
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        Assert.Empty(index["entries"]!.AsArray());
    }

    [Fact]
    public async Task Apply_ValidatedPlanRejectsLateWhitespaceCarrier()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));
        await ValidateRawPlanAsync(context);
        await context.FileSystem.WriteFileAtomicAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            " \r\n");
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        Assert.Contains("changed after validation", exception.Message, StringComparison.Ordinal);
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData(EffectMaterializationTestContext.PlayerEffectsPath)]
    [InlineData(EffectMaterializationTestContext.IdentityIndexPath)]
    public async Task Apply_ValidatedPlanRejectsLateLiteralNullAuthority(string path)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));
        await ValidateRawPlanAsync(context);
        await context.FileSystem.WriteFileAtomicAsync(path, "null");
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        Assert.Contains("changed after validation", exception.Message, StringComparison.Ordinal);
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Apply_ValidatedPlanRejectsLateWhitespaceCommand()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync(CreateNonResourceDefinition());
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateNonResourceApplyCommand()));
        await ValidateRawPlanAsync(context);
        await context.FileSystem.WriteFileAtomicAsync(
            EffectMaterializationTestContext.CommandPath,
            " \r\n");
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedEffectsAsync(backups));

        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task NormalizeSkills_UnchangedRootPreservesAcceptedMetadata()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string path = "game_state/player/skills_active.json";
        const string timestamp = "2026-08-15T06:40:00Z";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = "skill_metadata_unchanged",
                    ["skillName"] = "Стойка хранителя"
                }),
                ["_lastUpdated"] = timestamp
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        await context.Normalizer.NormalizeAccumulatedStateAsync(backups);

        var root = (await context.ReadJsonAsync(path))!.AsObject();
        Assert.Equal(timestamp, root["_lastUpdated"]!.GetValue<string>());
    }

    [Fact]
    public async Task NormalizeSkills_UpdatePreservesCurrentAcceptedMetadata()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string path = "game_state/player/skills_active.json";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = "skill_metadata_update",
                    ["skillName"] = "Стойка хранителя",
                    ["level"] = 1
                }),
                ["_lastUpdated"] = "2026-08-15T06:39:00Z"
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        const string acceptedTimestamp = "2026-08-15T06:41:00Z";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = "skill_metadata_update",
                    ["skillName"] = "Стойка хранителя",
                    ["level"] = 2
                }),
                ["_lastUpdated"] = acceptedTimestamp
            });

        await context.Normalizer.NormalizeAccumulatedStateAsync(backups);

        var root = (await context.ReadJsonAsync(path))!.AsObject();
        Assert.Equal(acceptedTimestamp, root["_lastUpdated"]!.GetValue<string>());
        Assert.Equal(2, root["activeSkillChanges"]![0]!["level"]!.GetValue<int>());
    }

    [Fact]
    public async Task Apply_NpcPreservesAdjacentWoundState()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync(CreateNonResourceDefinition());
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_test_healer"
                })
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath,
            new JsonObject
            {
                ["NPCWoundChanges"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_test_healer",
                    ["wounds"] = new JsonArray(new JsonObject
                    {
                        ["woundId"] = "wound_npc_arm",
                        ["severity"] = "moderate"
                    })
                }),
                ["_lastUpdated"] = "2026-08-15T00:00:00Z"
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateNonResourceApplyCommand("npc")));

        await ValidateRawPlanAsync(context);
        await context.NormalizeAcceptedEffectsAsync(backups);

        await AssertCanonicalCarrierPassesLegacyStatePhaseAsync(
            context,
            GameStateValidationPhase.NpcStateFiles,
            EffectMaterializationTestContext.NpcEffectsPath,
            EffectMaterializationTestContext.NpcEffectsPath);

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath))!.AsObject();
        var npc = Assert.IsType<JsonObject>(Assert.Single(root["entries"]!.AsArray()));
        Assert.Single(npc["activeEffects"]!.AsArray());
        Assert.Equal(
            "moderate",
            root["NPCWoundChanges"]![0]!["wounds"]![0]!["severity"]!.GetValue<string>());
        Assert.Equal("2026-08-15T00:00:00Z", root["_lastUpdated"]!.GetValue<string>());
    }

    [Fact]
    public async Task Apply_UsesValidatedSameTurnPlanAfterActorSourceNormalization()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string factionInitialId = "temp-faction-effect-source";
        const string npcInitialId = "npcref_test_healer";
        await context.WriteJsonAsync(
            "game_state/factions/faction_core.json",
            new JsonObject { ["factions"] = new JsonArray() });
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject { ["NPCsInScene"] = new JsonArray() });
        var canonicalLocation = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            MortalActorTestFixtures.DefaultLocationId,
            "Validated effect actor fixture location");
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(canonicalLocation));
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            "game_state/factions/faction_core.json",
            new JsonObject
            {
                ["factionDataChanges"] = new JsonArray(
                    EffectMaterializationTestFixture.CreateSameTurnMortalFaction(
                        factionInitialId))
            });
        var sameTurnActor = EffectMaterializationTestFixture.CreateSameTurnMortalActor(
            npcInitialId);
        sameTurnActor["resourceMaterialization"] = CreateResourceMaterialization(
            ("health", 60m));
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(sameTurnActor)
            });
        var command = EffectMaterializationTestFixture.CreateApplyCommand("npc");
        command["target"] = new JsonObject
        {
            ["kind"] = "npc",
            ["targetRef"] = npcInitialId
        };
        command["source"]!["kind"] = "faction";
        command["source"]!["sourceId"] = factionInitialId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var rawIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            rawIssues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, rawIssues.Select(issue => issue.Code)));

        await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(backups);

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath))!.AsObject();
        var npc = Assert.IsType<JsonObject>(Assert.Single(root["entries"]!.AsArray()));
        Assert.Equal(npcInitialId, npc["NPCId"]!.GetValue<string>());
        Assert.Single(npc["activeEffects"]!.AsArray());
        Assert.False(npc.ContainsKey("initialId"));
        Assert.Null(await context.ReadJsonAsync(EffectMaterializationTestContext.CommandPath));
    }

    [Theory]
    [InlineData("buff", "activeBuffs")]
    [InlineData("debuff", "activeDebuffs")]
    public async Task Apply_CombatantUsesCategoryCarrierAndPreservesSibling(
        string category,
        string expectedCollection)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreateNonResourceDefinition();
        definition["display"]!["category"] = category;
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(new JsonObject
                {
                    ["combatantId"] = EffectMaterializationTestFixture.CombatantId,
                    ["initiative"] = 17,
                    ["activeBuffs"] = new JsonArray(),
                    ["activeDebuffs"] = new JsonArray()
                })
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateNonResourceApplyCommand("combatant")));

        await ValidateRawPlanAsync(context);
        await context.NormalizeAcceptedEffectsAsync(backups);

        await AssertCanonicalCarrierPassesLegacyStatePhaseAsync(
            context,
            GameStateValidationPhase.WorldQuestCombatFactionStateFiles,
            EffectMaterializationTestContext.EnemyCombatantsPath,
            $"{EffectMaterializationTestContext.EnemyCombatantsPath}.enemiesData[0].{expectedCollection}");

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var combatant = Assert.IsType<JsonObject>(Assert.Single(root["enemiesData"]!.AsArray()));
        Assert.Equal(17, combatant["initiative"]!.GetValue<int>());
        Assert.Single(combatant[expectedCollection]!.AsArray());
        Assert.Empty(combatant[expectedCollection == "activeBuffs" ? "activeDebuffs" : "activeBuffs"]!.AsArray());
    }

    [Fact]
    public async Task Apply_AllyCombatantPublishesOnlyThePlannedBuffCollection()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = CreateNonResourceDefinition();
        definition["display"]!["category"] = "buff";
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.AllyCombatantsPath,
            new JsonObject
            {
                ["alliesData"] = new JsonArray(new JsonObject
                {
                    ["combatantId"] = EffectMaterializationTestFixture.CombatantId,
                    ["initiative"] = 9,
                    ["activeBuffs"] = new JsonArray(),
                    ["activeDebuffs"] = new JsonArray()
                })
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateNonResourceApplyCommand("combatant")));

        await ValidateRawPlanAsync(context);
        await context.NormalizeAcceptedEffectsAsync(backups);

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.AllyCombatantsPath))!.AsObject();
        var combatant = Assert.IsType<JsonObject>(Assert.Single(root["alliesData"]!.AsArray()));
        Assert.Equal(9, combatant["initiative"]!.GetValue<int>());
        Assert.Single(combatant["activeBuffs"]!.AsArray());
        Assert.Empty(combatant["activeDebuffs"]!.AsArray());
    }

    private static async Task ValidateRawPlanAsync(
        EffectMaterializationTestContext context)
    {
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(static issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(static issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
    }

    private static JsonObject CreateNonResourceDefinition()
    {
        return EffectMaterializationTestFixture.CreateDefinition("action_control");
    }

    private static JsonObject CreateNonResourceApplyCommand(string targetKind = "player")
    {
        var command = EffectMaterializationTestFixture.CreateApplyCommand(targetKind);
        command["parameters"] = new JsonObject();
        return command;
    }

    private static JsonObject CreateResourceMaterialization(
        params (string Key, decimal Maximum)[] resources) =>
        new()
        {
            ["resources"] = new JsonArray(resources
                .Select(resource => (JsonNode)new JsonObject
                {
                    ["resourceKey"] = resource.Key,
                    ["maximum"] = resource.Maximum
                })
                .ToArray())
        };

    private static async Task AssertCanonicalCarrierPassesLegacyStatePhaseAsync(
        EffectMaterializationTestContext context,
        GameStateValidationPhase phase,
        string stateFile,
        string governedPathPrefix)
    {
        var issues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(phase, new[] { stateFile }));
        var governedErrors = issues
            .Where(issue =>
                issue.Severity == IssueSeverity.Error &&
                issue.FilePath.StartsWith(governedPathPrefix, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            governedErrors.Length == 0,
            string.Join(Environment.NewLine, governedErrors.Select(issue =>
                $"{issue.Code} {issue.FilePath}: {issue.Message}")));
    }
}
