using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMaterializationValidationTests
{
    [Theory]
    [InlineData("playerActiveEffectsChanges")]
    [InlineData("NPCEffectChanges")]
    public async Task ResponseValidation_LegacyEffectRouteIsExplicitlyRejected(
        string legacyField)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var response = new JsonObject
        {
            [legacyField] = new JsonArray()
        };
        using var document = JsonDocument.Parse(response.ToJsonString());

        var issues = context.Validator.ValidateResponse(document.RootElement);

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_legacy_route_unsupported" &&
            string.Equals(
                issue.FilePath,
                $"response.{legacyField}",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task RawApply_PristineMissingCarrierAndIndexArePlannedWithoutMutation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task RawApply_TwoDistinctAcceptedTurnEventsCreateTwoTransitions()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var first = EffectMaterializationTestFixture.CreateApplyCommand();
        var second = EffectMaterializationTestFixture.CreateApplyCommand();
        second["eventRef"]!["authorityId"] = "turn_42_effect_2";
        second["reason"] = "Рана дала второе независимое осложнение.";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(first, second));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        await context.NormalizeAcceptedEffectsAsync(backups);

        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()));
        var eventRefs = entry["transitions"]!.AsArray()
            .OfType<JsonObject>()
            .Where(transition => transition["kind"]!.GetValue<string>() is "create" or "stack")
            .Select(transition => transition["eventRef"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(
            new[]
            {
                "turn_42:accepted_effect",
                "turn_42:accepted_effect:2"
            },
            eventRefs);
    }

    [Theory]
    [InlineData("npc")]
    [InlineData("combatant")]
    public async Task RawApply_DuplicateExactTargetAuthorityFailsClosed(
        string targetKind)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        JsonObject command;
        if (targetKind == "npc")
        {
            const string npcId = "npc_duplicate_effect_target";
            var first = MortalActorTestFixtures.CreateActor(npcId);
            var second = MortalActorTestFixtures.CreateActor(npcId);
            second["name"] = "Двойник цели";
            await context.WriteJsonAsync(
                "game_state/npcs/npc_core.json",
                new JsonObject
                {
                    ["NPCsInScene"] = new JsonArray(first, second)
                });
            command = EffectMaterializationTestFixture.CreateApplyCommand("npc");
            command["target"]!["targetId"] = npcId;
        }
        else
        {
            const string combatantId = "combatant_duplicate_effect_target";
            var enemy = EffectMaterializationTestFixture.CreateSameTurnCombatant(
                "combatant_ref_enemy_duplicate");
            enemy["combatantId"] = combatantId;
            enemy.Remove("combatantRef");
            var ally = EffectMaterializationTestFixture.CreateSameTurnCombatant(
                "combatant_ref_ally_duplicate");
            ally["combatantId"] = combatantId;
            ally.Remove("combatantRef");
            await context.WriteJsonAsync(
                EffectMaterializationTestContext.EnemyCombatantsPath,
                new JsonObject { ["enemiesData"] = new JsonArray(enemy) });
            await context.WriteJsonAsync(
                EffectMaterializationTestContext.AllyCombatantsPath,
                new JsonObject { ["alliesData"] = new JsonArray(ally) });
            command = EffectMaterializationTestFixture.CreateApplyCommand("combatant");
            command["target"]!["targetId"] = combatantId;
        }
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_target_authority_duplicate_target");
    }

    [Theory]
    [InlineData("new")]
    [InlineData("changed")]
    public async Task RawValidation_EmptyCombatantCannotAuthorClientIdentity(
        string mutation)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var before = EffectMaterializationTestFixture.CreateSameTurnCombatant(
            "combatant_ref_identity_guard");
        if (mutation == "new")
        {
            await context.WriteJsonAsync(
                EffectMaterializationTestContext.EnemyCombatantsPath,
                new JsonObject { ["enemiesData"] = new JsonArray() });
        }
        else
        {
            before["combatantId"] = "combatant_pre_turn_identity";
            before.Remove("combatantRef");
            await context.WriteJsonAsync(
                EffectMaterializationTestContext.EnemyCombatantsPath,
                new JsonObject { ["enemiesData"] = new JsonArray(before.DeepClone()) });
        }
        await context.CaptureValidatedPendingSnapshotAsync();
        var current = before.DeepClone().AsObject();
        current["combatantId"] = mutation == "new"
            ? "combatant_gm_authored_new"
            : "combatant_gm_authored_changed";
        current.Remove("combatantRef");
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray(current) });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_target_combatant_identity_direct_mutation");
    }

    [Fact]
    public async Task RawApply_DirectCarrierMutationFailsClosed()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EmptyPlayerCarrier());
        await context.CaptureValidatedPendingSnapshotAsync();
        var forged = EffectMaterializationTestFixture.CreateCanonicalEffect();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(forged)
            });

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "effect_materialization_direct_carrier_mutation");
    }

    [Fact]
    public async Task RawValidation_CombatantReorderPreservesEffectCarrierContinuity()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var first = EffectMaterializationTestFixture.CreateSameTurnCombatant(
            "combatant_ref_reorder_first");
        first.Remove("combatantRef");
        first["combatantId"] = "combatant_reorder_first";
        first["activeBuffs"] = new JsonArray(new JsonObject
        {
            ["effectId"] = "effect_reorder_first"
        });
        var second = EffectMaterializationTestFixture.CreateSameTurnCombatant(
            "combatant_ref_reorder_second");
        second.Remove("combatantRef");
        second["combatantId"] = "combatant_reorder_second";
        second["activeDebuffs"] = new JsonArray(new JsonObject
        {
            ["effectId"] = "effect_reorder_second"
        });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(
                    first.DeepClone(),
                    second.DeepClone())
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(
                    second.DeepClone(),
                    first.DeepClone())
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "effect_materialization_direct_carrier_mutation");
    }

    [Fact]
    public async Task RawApply_DirectIdentityIndexMutationFailsClosed()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "effect_identity_direct_mutation");
    }

    [Theory]
    [InlineData("raw")]
    [InlineData("canonical")]
    public async Task Validation_NonObjectCarrierNeverCollapsesToPristineMissing(
        string phase)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        if (phase == "raw")
            await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonArray(new JsonObject { ["legacyEffect"] = "must-not-be-lost" }));

        var issues = phase == "raw"
            ? await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync()
            : await context.Validator.ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_invalid_carrier_root" &&
            string.Equals(
                issue.FilePath,
                EffectMaterializationTestContext.PlayerEffectsPath,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task CanonicalValidation_NonObjectIdentityIndexNeverBecomesEmptyIndex()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            new JsonArray(new JsonObject { ["legacyIdentity"] = "must-not-be-lost" }));

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_identity_invalid_root" &&
            string.Equals(
                issue.FilePath,
                EffectMaterializationTestContext.IdentityIndexPath,
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("game_state/player/effects.json", "effect_materialization_invalid_carrier_root", "")]
    [InlineData("game_state/player/effects.json", "effect_materialization_invalid_carrier_root", " \r\n")]
    [InlineData("game_state/effects/effect_identity_index.json", "effect_identity_invalid_root", "")]
    [InlineData("game_state/effects/effect_identity_index.json", "effect_identity_invalid_root", " \r\n")]
    [InlineData("game_state/player/wounds.json", "effect_materialization_invalid_source_root", "")]
    [InlineData("game_state/player/wounds.json", "effect_materialization_invalid_source_root", " \r\n")]
    public async Task CanonicalValidation_PresentEmptyAuthorityIsMalformed(
        string path,
        string expectedCode,
        string contents)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.FileSystem.WriteFileAtomicAsync(path, contents);

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == expectedCode &&
            string.Equals(issue.FilePath, path, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \r\n")]
    public async Task RawValidation_PresentEmptyCommandIsMalformed(string contents)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.FileSystem.WriteFileAtomicAsync(
            EffectMaterializationTestContext.CommandPath,
            contents);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.True(
            issues.Any(issue =>
                issue.Code == "effect_plan_command_invalid" &&
                string.Equals(
                    issue.FilePath,
                    EffectMaterializationTestContext.CommandPath,
                    StringComparison.Ordinal)),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
    }

    [Fact]
    public async Task RawApply_DuplicateCommandPropertyFailsBeforePlanningAndWritesNothing()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.FileSystem.WriteFileAtomicAsync(
            EffectMaterializationTestContext.CommandPath,
            """
            {
              "effectChanges": [
                {
                  "operation": "apply",
                  "operation": "apply",
                  "target": { "kind": "player", "targetId": "player_current" },
                  "source": {
                    "kind": "wound",
                    "sourceId": "wound_test_torn_side",
                    "definitionKey": "bleeding_consequence"
                  },
                  "parameters": { "amount": 3 },
                  "eventRef": { "kind": "accepted_turn", "authorityId": "turn_42" },
                  "reason": "Рана снова открылась."
                }
              ],
              "effectResolutionReceipts": []
            }
            """);
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath);

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "effect_plan_duplicate_property");
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath);
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("carrier")]
    [InlineData("index")]
    [InlineData("source")]
    public async Task RawValidation_DuplicateEffectAuthorityPropertyFailsClosed(
        string surface)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var path = surface switch
        {
            "carrier" => EffectMaterializationTestContext.PlayerEffectsPath,
            "index" => EffectMaterializationTestContext.IdentityIndexPath,
            "source" => "game_state/player/skills_active.json",
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null)
        };
        var json = surface switch
        {
            "carrier" =>
                """{ "schemaVersion": 1, "activeEffects": [], "activeEffects": [] }""",
            "index" =>
                """{ "schemaVersion": 1, "entries": [], "entries": [] }""",
            "source" =>
                """{ "entries": [], "entries": [{ "skillId": "skill_duplicate" }] }""",
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null)
        };
        await context.FileSystem.WriteFileAtomicAsync(path, json);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_duplicate_property" &&
            issue.FilePath.StartsWith(path, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("source", "effect_source_selector_unresolved")]
    [InlineData("target", "effect_target_selector_unresolved")]
    public async Task RawApply_UnresolvedSourceOrTargetFailsWithoutCanonicalWrites(
        string selector,
        string expectedCode)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        if (selector == "source")
            command["source"]!["sourceId"] = "wound_unknown";
        else
            command["target"]!["targetId"] = "player_unknown";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath);

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == expectedCode);
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath);
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("display")]
    [InlineData("components")]
    [InlineData("lifetime")]
    [InlineData("stacking")]
    [InlineData("removal")]
    public async Task CanonicalValidation_MissingCompleteEnvelopeSectionFails(string field)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect.Remove(field);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect)
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            });

        var issues = await context.Validator.ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "effect_materialization_missing_field");
    }

    [Theory]
    [InlineData("ownerId")]
    [InlineData("stackKey")]
    [InlineData("createdEventRef")]
    [InlineData("lastTransitionTurn")]
    public async Task CanonicalValidation_IndexOwnerStackAndChronologyMustMatchCarrier(
        string forgedField)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        var entry = index["entries"]!.AsArray()[0]!.AsObject();
        if (forgedField == "ownerId")
            entry["owner"]!["ownerId"] = "player_forged";
        else if (forgedField == "stackKey")
            entry["stackCoordinate"]!["stackKey"] = "forged_stack";
        else if (forgedField == "createdEventRef")
            effect["chronology"]!["createdEventRef"] = "turn_42:forged_event";
        else
            effect["chronology"]!["lastTransitionTurn"] = 43;

        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect)
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            index);

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_index_carrier_mismatch");
    }

    [Fact]
    public async Task CanonicalValidation_LiveEffectSourceMustResolveAgainstFinalSourceCatalog()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect)
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_selector_unresolved" &&
            issue.FilePath.EndsWith(".source", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CanonicalValidation_InactiveExactSourceCanRetainNoChangeEffect()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["removal"]!["onSourceLoss"] = "no_change";
        await context.SeedPlayerWoundSourceAsync(definition);
        var wounds = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath))!.AsArray();
        wounds[0]!["isHealed"] = true;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            wounds);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["removal"]!["onSourceLoss"] = "no_change";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect)
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Theory]
    [InlineData("display")]
    [InlineData("components")]
    [InlineData("lifetime")]
    [InlineData("stacking")]
    [InlineData("triggers")]
    [InlineData("removal")]
    [InlineData("links")]
    public async Task CanonicalValidation_SourceOwnedMechanicsMustMatchResolvedDefinition(
        string forgedSection)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        switch (forgedSection)
        {
            case "display":
                effect["display"]!["name"] = "Подменённое кровотечение";
                break;
            case "components":
                effect["components"]![0]!["priority"] = 101;
                break;
            case "lifetime":
                effect["lifetime"]!["advancePhase"] = "owner_turn_start";
                break;
            case "stacking":
                effect["stacking"]!["maxStacks"] = 4;
                break;
            case "triggers":
                effect["triggers"]![0]!["priority"] = 101;
                break;
            case "removal":
                effect["removal"]!["onSourceLoss"] = "suspend";
                break;
            case "links":
                effect["links"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "wound",
                    ["targetId"] = "wound_forged",
                    ["role"] = "context"
                });
                break;
        }
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect)
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_source_definition_mismatch" &&
            issue.FilePath.Contains(forgedSection, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CanonicalValidation_SourceBoundedParametersAndRuntimeCountersRemainValid()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["components"]![0]!["payload"]!["amount"] = 4;
        effect["lifetime"]!["remainingTurns"] = 2;
        effect["stacking"]!["currentStacks"] = 2;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect)
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task CanonicalValidation_CombatCategoryMustMatchPhysicalCollection()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("combatant");
        var combatant = EffectMaterializationTestFixture.CreateSameTurnCombatant(
            EffectMaterializationTestFixture.CombatantRef);
        combatant.Remove("combatantRef");
        combatant["combatantId"] = EffectMaterializationTestFixture.CombatantId;
        combatant["activeBuffs"] = new JsonArray(effect);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray(combatant) });
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        index["entries"]![0]!["owner"]!["collection"] = "activeBuffs";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            index);

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_combat_category_collection_mismatch");
    }

    [Fact]
    public async Task CanonicalValidation_LiveEffectTargetMustResolveAgainstFinalTargetCatalog()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["target"]!["targetId"] = "player_missing";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect)
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_target_selector_unresolved" &&
            issue.FilePath.EndsWith(".target", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CanonicalValidation_OrphanNpcCarrierCannotSelfAuthorizeDeletedTarget()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("npc");
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_test_healer",
                    ["activeEffects"] = new JsonArray(effect)
                })
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_target_selector_unresolved" &&
            issue.FilePath.EndsWith(".target", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CanonicalValidation_CombatantWithoutClientIdentityCannotHideLegacyEffects()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_legacy_raider",
                    ["activeBuffs"] = new JsonArray(new JsonObject
                    {
                        ["effectType"] = "DamageReduction",
                        ["value"] = 2
                    }),
                    ["activeDebuffs"] = new JsonArray()
                })
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_legacy_carrier_unsupported" &&
            issue.FilePath.Contains("enemiesData[0]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CanonicalValidation_NestedNpcSkillIdCannotAuthorizeNpcTarget()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_real_healer",
                    ["activeSkills"] = new JsonArray(new JsonObject
                    {
                        ["id"] = "nested_skill_not_an_npc"
                    })
                })
            });
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("npc");
        effect["target"]!["targetId"] = "nested_skill_not_an_npc";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "nested_skill_not_an_npc",
                    ["activeEffects"] = new JsonArray(effect)
                })
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_target_selector_unresolved" &&
            issue.FilePath.EndsWith(".target", StringComparison.Ordinal));
    }

    private static JsonObject EmptyPlayerCarrier() =>
        new()
        {
            ["schemaVersion"] = 1,
            ["activeEffects"] = new JsonArray()
        };
}
