using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMaterializationValidationTests
{
    [Theory]
    [InlineData("[INK_FEATHER_ACTION: FATE_SHIELD] Подтвердить покупку.", false)]
    [InlineData("Обычное действие без покупки Щита Судьбы.", true)]
    public async Task RawValidation_BuiltInFateShieldRequiresExactSealedAction(
        string playerAction,
        bool expectMissingAuthority)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.CaptureValidatedPendingSnapshotAsync(playerAction: playerAction);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                new JsonObject
                {
                    ["operation"] = "apply",
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    },
                    ["source"] = new JsonObject
                    {
                        ["kind"] = EffectBuiltInSourceCatalog.FateShieldSourceKind,
                        ["sourceId"] = EffectBuiltInSourceCatalog.FateShieldSourceId,
                        ["definitionKey"] = EffectBuiltInSourceCatalog.FateShieldDefinitionKey
                    },
                    ["parameters"] = new JsonObject(),
                    ["eventRef"] = new JsonObject
                    {
                        ["kind"] = "accepted_turn",
                        ["authorityId"] = "turn_42"
                    },
                    ["reason"] = "Игрок оплатил Щит Судьбы Чернильными Перьями."
                }));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Equal(
            expectMissingAuthority,
            issues.Any(static issue =>
                issue.Code == "effect_source_application_authority_missing"));
        if (!expectMissingAuthority)
        {
            Assert.DoesNotContain(
                issues,
                static issue => issue.Severity == IssueSeverity.Error);
        }
    }

    [Theory]
    [InlineData("activeSkillChanges")]
    [InlineData("playerWoundChanges")]
    [InlineData("UpdateNPCs")]
    [InlineData("UpdateQuests")]
    [InlineData("enemiesData")]
    [InlineData("factionDataChanges")]
    [InlineData("worldEventsLog")]
    public async Task ResponseValidation_MalformedOwnedDefinitionFailsWithoutEffectCommand(
        string responseField)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition.Remove("removal");
        var response = new JsonObject
        {
            [responseField] = new JsonArray(new JsonObject
            {
                ["activeEffectDefinitions"] = new JsonArray(definition)
            })
        };
        using var document = System.Text.Json.JsonDocument.Parse(response.ToJsonString());

        var issues = context.Validator.ValidateResponse(document.RootElement);

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_missing_field" &&
            issue.FilePath.Contains(responseField, StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue =>
            issue.Code == "effect_plan_input_invalid" ||
            issue.Code == "effect_materialization_snapshot_required");
    }

    [Fact]
    public async Task RawValidation_CommandlessSourceRejectsIncompatibleRegisteredPredicate()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "equipped",
            ["onSourceLoss"] = "expire"
        };
        await context.WriteJsonAsync(
            "game_state/player/skills_active.json",
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = "skill_incompatible_equipped_predicate",
                    ["activeEffectDefinitions"] = new JsonArray(definition)
                })
            });
        await context.CaptureValidatedPendingSnapshotAsync();

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_predicate_incompatible");
    }

    [Fact]
    public async Task RawValidation_CommandlessDefinitionOwnerRequiresExactSourceIdentity()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            "game_state/player/skills_active.json",
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(new JsonObject
                {
                    ["skillName"] = "Навык без устойчивой идентичности",
                    ["activeEffectDefinitions"] = new JsonArray(
                        EffectMaterializationTestFixture.CreateDefinition())
                })
            });
        await context.CaptureValidatedPendingSnapshotAsync();

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_authority_invalid_owner_identity");
    }

    [Fact]
    public async Task ResponseValidation_MalformedLocationDefinitionFailsAtRawLocationBoundary()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition.Remove("removal");
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["activeEffectDefinitions"] = new JsonArray(definition);
        var response = new JsonObject
        {
            ["worldMapUpdates"] = new JsonObject
            {
                ["newLocations"] = new JsonArray(location)
            }
        };
        using var document = System.Text.Json.JsonDocument.Parse(response.ToJsonString());

        var issues = context.Validator.ValidateResponse(document.RootElement);

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_missing_field" &&
            issue.FilePath.Contains("newLocations[0].activeEffectDefinitions", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NpcStateValidation_MalformedFateCardDefinitionFailsAtOwnerBoundary()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition.Remove("removal");
        var actor = MortalActorTestFixtures.CreateActor("npc_effect_card_owner");
        actor["fateCards"] = new JsonArray(new JsonObject
        {
            ["cardId"] = "card_effect_owner",
            ["name"] = "Карта незаживающего следа",
            ["image_prompt"] = "dark fate card with a crimson scar, realistic engraving",
            ["description"] = "Рана напоминает о себе.",
            ["isUnlocked"] = true,
            ["activeEffectDefinitions"] = new JsonArray(definition)
        });
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject { ["NPCsInScene"] = new JsonArray(actor) });

        var issues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(
                GameStateValidationPhase.NpcStateFiles,
                new[] { "game_state/npcs/npc_core.json" }));

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_missing_field" &&
            issue.FilePath.Contains("fateCards[0]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CombatStateValidation_MalformedActionDefinitionFailsAtOwnerBoundary()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition.Remove("removal");
        var combatant = EffectMaterializationTestFixture.CreateSameTurnCombatant(
            "combatant_effect_action_owner");
        combatant["actions"] = new JsonArray(new JsonObject
        {
            ["combatActionId"] = "action_effect_owner",
            ["actionName"] = "Рваный выпад",
            ["actionCost"] = "Main",
            ["effects"] = new JsonArray(new JsonObject
            {
                ["effectType"] = "Damage",
                ["value"] = "10%",
                ["description"] = "Удар раскрывает старую рану."
            }),
            ["activeEffectDefinitions"] = new JsonArray(definition)
        });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray(combatant) });

        var issues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(
                GameStateValidationPhase.WorldQuestCombatFactionStateFiles,
                new[] { EffectMaterializationTestContext.EnemyCombatantsPath }));

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_missing_field" &&
            issue.FilePath.Contains("actions[0]", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("specialArts")]
    [InlineData("fateCards")]
    public async Task AfterlifeProfileValidation_MalformedNestedDefinitionFailsAtOwnerBoundary(
        string collection)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var root = JsonNode.Parse(
            AfterlifeEntityProfileValidationTests.BuildValidProfileJson())!.AsObject();
        var profile = root[AfterlifeEntityProfileState.ProfilesProperty]![0]!.AsObject();
        if (collection == "fateCards")
        {
            profile["fateCards"] = new JsonArray(new JsonObject
            {
                ["cardId"] = "card_effect_owner",
                ["nameRu"] = "Карта незаживающего следа",
                ["status"] = "unlocked",
                ["storyMeaning"] = "След раны остаётся в памяти души.",
                ["unlockConditions"] = new JsonObject
                {
                    ["summary"] = "Признать полученную рану."
                },
                ["appliedAtTurn"] = 41,
                ["evidenceSummary"] = "Душа приняла след.",
                ["guardianEffects"] = new JsonArray(new JsonObject
                {
                    ["summary"] = "След остаётся видимым."
                })
            });
        }
        var owner = profile[collection]!.AsArray()[0]!.AsObject();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["allowedRealms"] = new JsonArray("shining_abode");
        definition.Remove("removal");
        owner["activeEffectDefinitions"] = new JsonArray(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath,
            root);

        var issues = await context.Validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.AfterlifeEntityProfile);

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_missing_field" &&
            issue.FilePath.Contains(collection, StringComparison.Ordinal));
    }

    public static IEnumerable<object[]> CanonicalSourceCases()
    {
        yield return new object[] { "game_state/player/skills_active.json", "skill", "skillId", "skill_test_bleeding" };
        yield return new object[] { "game_state/inventory/items.json", "item", "itemId", "itm_test_bleeding" };
        yield return new object[] { "game_state/quests/regular_quests.json", "quest", "questId", "quest_test_bleeding" };
        yield return new object[] { "game_state/world/world_map.json", "location", "locationId", "loc_test_bleeding" };
        yield return new object[] { "game_state/factions/faction_core.json", "faction", "factionId", "faction_test_bleeding" };
        yield return new object[] { "game_state/world/world_events.json", "world_event", "eventId", "event_test_bleeding" };
        yield return new object[] { "game_state/npcs/npc_core.json", "combat_action", "combatActionId", "action_test_bleeding" };
    }

    [Theory]
    [MemberData(nameof(CanonicalSourceCases))]
    public async Task RawApply_ResolvesExactDefinitionFromCanonicalSourceBoundary(
        string path,
        string sourceKind,
        string identityField,
        string sourceId)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var source = new JsonObject
        {
            [identityField] = sourceId,
            ["activeEffectDefinitions"] = new JsonArray(
                EffectMaterializationTestFixture.CreateDefinition())
        };
        if (sourceKind == "quest")
            source["status"] = "Active";
        if (sourceKind == "world_event")
            source["isActive"] = true;
        await context.WriteJsonAsync(
            path,
            CreateCanonicalSourceRoot(path, source));
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = sourceKind;
        command["source"]!["sourceId"] = sourceId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
    }

    [Fact]
    public async Task RawApply_SelectedCurrentLocationHasOneCanonicalSourceAuthority()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string locationId = "loc_current_effect_source";
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            locationId,
            "Двор под багряным дождём",
            discoveryTier: "visited");
        location["activeEffectDefinitions"] = new JsonArray(
            EffectMaterializationTestFixture.CreateDefinition());
        MortalLocationTestFixture.ResealCanonicalLocation(location);
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(location));
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(location));
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["source"] = new JsonObject
        {
            ["kind"] = "location",
            ["sourceId"] = locationId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task RawApply_NonCurrentDuplicateHazardIdRemainsAmbiguousAuthority()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string hazardId = "hazard_shared_between_locations";
        var current = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_current_hazard_authority",
            "Текущий багряный двор",
            discoveryTier: "visited");
        current["hazards"] = new JsonArray(new JsonObject
        {
            ["hazardId"] = hazardId,
            ["status"] = "active",
            ["activeEffectDefinitions"] = new JsonArray(
                EffectMaterializationTestFixture.CreateDefinition())
        });
        MortalLocationTestFixture.ResealCanonicalLocation(current);
        var remote = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_remote_hazard_authority",
            "Дальний багряный двор",
            discoveryTier: "rumored");
        remote["hazards"] = new JsonArray(new JsonObject
        {
            ["hazardId"] = hazardId,
            ["status"] = "active",
            ["activeEffectDefinitions"] = new JsonArray(
                EffectMaterializationTestFixture.CreateDefinition())
        });
        MortalLocationTestFixture.ResealCanonicalLocation(remote);
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(current, remote));
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(current));
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["source"] = new JsonObject
        {
            ["kind"] = "hazard",
            ["sourceId"] = hazardId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_authority_duplicate_source");
    }

    [Theory]
    [InlineData("Chaos Sea")]
    [InlineData("Море Хаоса")]
    public async Task RawApply_ValidChaosSeaProfileSourceDoesNotBlockMortalEffect(
        string profileRealm)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerSkillSourceAsync();
        var profileRoot = JsonNode.Parse(
            AfterlifeEntityProfileValidationTests.BuildValidProfileJson())!.AsObject();
        var profile = profileRoot[AfterlifeEntityProfileState.ProfilesProperty]![0]!
            .AsObject();
        profile["realm"] = profileRealm;
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["allowedRealms"] = new JsonArray("chaos_sea");
        profile["fateCards"] = new JsonArray(new JsonObject
        {
            ["cardId"] = "card_chaos_bleeding",
            ["nameRu"] = "Кровавый след",
            ["status"] = "unlocked",
            ["storyMeaning"] = "Карта удерживает память о ране.",
            ["unlockConditions"] = new JsonObject
            {
                ["summary"] = "Признать слабость."
            },
            ["appliedAtTurn"] = 41,
            ["evidenceSummary"] = "Душа приняла след раны.",
            ["guardianEffects"] = new JsonArray(new JsonObject
            {
                ["summary"] = "Карта сохраняет след раны."
            }),
            ["activeEffectDefinitions"] = new JsonArray(definition)
        });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath,
            profileRoot);

        var profileIssues = await context.Validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.AfterlifeEntityProfile);
        Assert.DoesNotContain(profileIssues, issue => issue.Severity == IssueSeverity.Error);

        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateMaterializableApplyCommand()));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Theory]
    [InlineData("game_state/player/wounds.json", "wound", "wound_same_turn_effect", false)]
    [InlineData("game_state/quests/regular_quests.json", "quest", "quest_same_turn_effect", true)]
    [InlineData("game_state/world/world_events.json", "world_event", "event_same_turn_effect", true)]
    public async Task RawApply_ComposesOnlyMaterializableValidatedSameTurnSource(
        string path,
        string sourceKind,
        string sourceId,
        bool expectMaterializable)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(path, CreateEmptySameTurnOwnerRoot(path));
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            path,
            CreateCompleteSameTurnOwnerRoot(path, sourceId));
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = sourceKind;
        command["source"]!["sourceId"] = sourceId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Equal(
            !expectMaterializable,
            issues.Any(issue => issue.Code == "effect_source_selector_unresolved"));
        if (expectMaterializable)
        {
            Assert.DoesNotContain(
                issues,
                issue => issue.Severity == IssueSeverity.Error);
        }
    }

    [Fact]
    public async Task RawApply_ExistingSourceUsesValidatedComposedDefinitionReplacement()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var beforeDefinition = EffectMaterializationTestFixture.CreateDefinition();
        beforeDefinition["definitionKey"] = "old_definition";
        await context.SeedPlayerSkillSourceAsync(beforeDefinition);
        await context.CaptureValidatedPendingSnapshotAsync();
        var afterDefinition = EffectMaterializationTestFixture.CreateDefinition();
        afterDefinition["definitionKey"] = "accepted_definition";
        await context.SeedPlayerSkillSourceAsync(afterDefinition);
        var command = CreateMaterializableApplyCommand();
        command["source"]!["definitionKey"] = "accepted_definition";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task RawApply_ExistingSourceRemovalDoesNotFallBackToPreTurnDefinition()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerSkillSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.MaterializableSkillPath,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(),
                ["removeActiveSkills"] = new JsonArray(
                    "Кровавый след")
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateMaterializableApplyCommand()));

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_selector_unresolved");
    }

    [Fact]
    public async Task RawApply_PartialQuestUpdatePreservesComposedDefinitionAuthority()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string questId = "quest_partial_effect_source";
        var quest = CreateCanonicalQuestSource(questId, "Active");
        await context.WriteJsonAsync(
            "game_state/quests/regular_quests.json",
            new JsonObject { ["quests"] = new JsonArray(quest.DeepClone()) });
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            "game_state/quests/regular_quests.json",
            new JsonObject
            {
                ["quests"] = new JsonArray(quest.DeepClone()),
                ["UpdateQuests"] = new JsonArray(new JsonObject
                {
                    ["questId"] = questId,
                    ["newDetailsLogEntry"] = "#[42]. След подтверждён."
                })
            });
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "quest";
        command["source"]!["sourceId"] = questId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void SameTurnSkillPatchPreservesUntouchedExactSourceAuthority()
    {
        const string path = "game_state/player/skills_active.json";
        const string untouchedSkillId = "skill_untouched_effect_source";
        const string updatedSkillId = "skill_updated_effect_source";
        var before = new JsonObject
        {
            ["activeSkillChanges"] = new JsonArray(
                new JsonObject
                {
                    ["skillId"] = untouchedSkillId,
                    ["skillName"] = "Нетронутый навык",
                    ["activeEffectDefinitions"] = new JsonArray(
                        EffectMaterializationTestFixture.CreateDefinition())
                },
                new JsonObject
                {
                    ["skillId"] = updatedSkillId,
                    ["skillName"] = "Изменяемый навык",
                    ["activeEffectDefinitions"] = new JsonArray(
                        EffectMaterializationTestFixture.CreateDefinition())
                })
        };
        var accepted = new JsonObject
        {
            ["activeSkillChanges"] = new JsonArray(new JsonObject
            {
                ["skillId"] = updatedSkillId,
                ["skillName"] = "Изменённый навык",
                ["activeEffectDefinitions"] = new JsonArray(
                    EffectMaterializationTestFixture.CreateDefinition())
            })
        };

        var exports = EffectAcceptedTurnInputComposer
            .CollectValidatedSameTurnOwnerExports(
                new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
                {
                    [path] = before
                },
                new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
                {
                    [path] = accepted
                });

        Assert.Contains(exports.Sources, source =>
            source.Kind == "skill" &&
            source.SourceId == untouchedSkillId &&
            source.Definitions.Count == 1);
    }

    [Fact]
    public void SameTurnSkillRemovalByExactContractNameRemovesSourceAuthority()
    {
        const string path = "game_state/player/skills_active.json";
        const string skillId = "skill_removed_effect_source";
        const string skillName = "Уходящий кровавый след";
        var before = new JsonObject
        {
            ["activeSkillChanges"] = new JsonArray(new JsonObject
            {
                ["skillId"] = skillId,
                ["skillName"] = skillName,
                ["activeEffectDefinitions"] = new JsonArray(
                    EffectMaterializationTestFixture.CreateDefinition())
            })
        };
        var accepted = new JsonObject
        {
            ["activeSkillChanges"] = new JsonArray(),
            ["removeActiveSkills"] = new JsonArray(skillName)
        };

        var exports = EffectAcceptedTurnInputComposer
            .CollectValidatedSameTurnOwnerExports(
                new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
                {
                    [path] = before
                },
                new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
                {
                    [path] = accepted
                });

        Assert.DoesNotContain(exports.Sources, source =>
            source.Kind == "skill" && source.SourceId == skillId);
        Assert.Contains(
            new EffectSourceOwnerKey("mortal_world", "skill", skillId),
            exports.ReplacedSourceOwners);
    }

    [Fact]
    public async Task RawApply_CompleteSkillReplacementCannotRetainOmittedEffectDefinition()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string path = "game_state/player/skills_active.json";
        const string skillId = "skill_replaced_without_effect_source";
        var original = CreateValidActiveSkill(
            skillId,
            "Погасший кровавый след",
            EffectMaterializationTestFixture.CreateDefinition());
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(original.DeepClone()),
                ["removeActiveSkills"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var replacement = CreateValidActiveSkill(
            skillId,
            "Погасший кровавый след");
        replacement["skillDescription"] =
            "Полное новое состояние без materializable effect definition.";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(replacement),
                ["removeActiveSkills"] = new JsonArray()
            });
        var command = CreateMaterializableApplyCommand();
        command["source"] = new JsonObject
        {
            ["kind"] = "skill",
            ["sourceId"] = skillId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_selector_unresolved");
    }

    [Fact]
    public async Task Normalize_CompleteSkillReplacementRemovesOmittedEffectDefinition()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string path = "game_state/player/skills_active.json";
        const string skillId = "skill_normalized_without_effect_source";
        var original = CreateValidActiveSkill(
            skillId,
            "Угасший след",
            EffectMaterializationTestFixture.CreateDefinition());
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(original.DeepClone()),
                ["removeActiveSkills"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var replacement = CreateValidActiveSkill(skillId, "Угасший след");
        replacement["skillDescription"] = "Полное новое состояние навыка.";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(replacement),
                ["removeActiveSkills"] = new JsonArray()
            });

        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.Normalizer.NormalizeAccumulatedStateAsync(backups);

        var persisted = (await context.ReadJsonAsync(path))!["activeSkillChanges"]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(skill => skill["skillId"]!.GetValue<string>() == skillId);
        Assert.False(persisted.ContainsKey("activeEffectDefinitions"));
        Assert.Equal(
            "Полное новое состояние навыка.",
            persisted["skillDescription"]!.GetValue<string>());
    }

    [Fact]
    public async Task RawValidation_SkillCompositionRejectsAmbiguousExactNameRemoval()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string path = "game_state/player/skills_active.json";
        const string sharedName = "Два одинаковых следа";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(
                    CreateValidActiveSkill("skill_duplicate_name_a", sharedName),
                    CreateValidActiveSkill("skill_duplicate_name_b", sharedName)),
                ["removeActiveSkills"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(),
                ["removeActiveSkills"] = new JsonArray(sharedName)
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_skill_composition_selector_ambiguous");
    }

    [Fact]
    public async Task RawValidation_SkillCompositionRejectsConfusableRemovalSelector()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string path = "game_state/player/skills_active.json";
        const string exactName = "Bleeding Ward";
        const string confusableName = "Bleedіng Ward";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(
                    CreateValidActiveSkill("skill_confusable_name", exactName)),
                ["removeActiveSkills"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(),
                ["removeActiveSkills"] = new JsonArray(confusableName)
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_skill_composition_selector_confusable");
    }

    [Fact]
    public async Task RawValidation_SkillCompositionRejectsAmbiguousNameOnlyUpdate()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string path = "game_state/player/skills_active.json";
        const string sharedName = "Неоднозначный выпад";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(
                    CreateValidActiveSkill("skill_ambiguous_update_a", sharedName),
                    CreateValidActiveSkill("skill_ambiguous_update_b", sharedName)),
                ["removeActiveSkills"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var update = CreateValidActiveSkill("skill_removed_for_name_update", sharedName);
        update.Remove("skillId");
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(update),
                ["removeActiveSkills"] = new JsonArray()
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_skill_composition_update_ambiguous");
    }

    [Fact]
    public async Task RawApply_SkillPatchPersistsComposedSourceBeforeEffectPublication()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string path = "game_state/player/skills_active.json";
        const string sourceSkillId = "skill_persisted_effect_source";
        const string updatedSkillId = "skill_persisted_unrelated_update";
        var sourceSkill = CreateValidActiveSkill(
            sourceSkillId,
            "Кровавый след",
            EffectMaterializationTestFixture.CreateDefinition());
        var updatedSkill = CreateValidActiveSkill(
            updatedSkillId,
            "Сторожевой выпад");
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(
                    sourceSkill.DeepClone(),
                    updatedSkill.DeepClone()),
                ["removeActiveSkills"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var changedSkill = updatedSkill.DeepClone().AsObject();
        changedSkill["skillDescription"] = "Навык B изменён в принятом ходе.";
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(changedSkill),
                ["removeActiveSkills"] = new JsonArray()
            });
        var command = CreateMaterializableApplyCommand();
        command["source"] = new JsonObject
        {
            ["kind"] = "skill",
            ["sourceId"] = sourceSkillId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(
            itemIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var rawIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(rawIssues, issue => issue.Severity == IssueSeverity.Error);

        await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(backups);
        var postIssues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();
        Assert.DoesNotContain(postIssues, issue => issue.Severity == IssueSeverity.Error);
        var persistedSkills = (await context.ReadJsonAsync(path))!["activeSkillChanges"]!
            .AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Contains(persistedSkills, skill =>
            skill["skillId"]!.GetValue<string>() == sourceSkillId);
        Assert.Contains(persistedSkills, skill =>
            skill["skillId"]!.GetValue<string>() == updatedSkillId &&
            skill["skillDescription"]!.GetValue<string>() ==
            "Навык B изменён в принятом ходе.");
        var carrier = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Single(carrier["activeEffects"]!.AsArray());
    }

    [Fact]
    public async Task RawApply_PartialQuestCompletionUsesComposedInactiveState()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string questId = "quest_completed_by_patch_effect_source";
        var quest = CreateCanonicalQuestSource(questId, "Active");
        await context.WriteJsonAsync(
            "game_state/quests/regular_quests.json",
            new JsonObject { ["quests"] = new JsonArray(quest.DeepClone()) });
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            "game_state/quests/regular_quests.json",
            new JsonObject
            {
                ["quests"] = new JsonArray(quest.DeepClone()),
                ["UpdateQuests"] = new JsonArray(new JsonObject
                {
                    ["questId"] = questId,
                    ["status"] = "Completed"
                })
            });
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "quest";
        command["source"]!["sourceId"] = questId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "effect_source_inactive");
    }

    [Fact]
    public async Task RawApply_PartialQuestDefinitionRemovalUsesComposedAfterState()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string questId = "quest_definition_removed_by_patch";
        var quest = CreateCanonicalQuestSource(questId, "Active");
        await context.WriteJsonAsync(
            "game_state/quests/regular_quests.json",
            new JsonObject { ["quests"] = new JsonArray(quest.DeepClone()) });
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            "game_state/quests/regular_quests.json",
            new JsonObject
            {
                ["quests"] = new JsonArray(quest.DeepClone()),
                ["UpdateQuests"] = new JsonArray(new JsonObject
                {
                    ["questId"] = questId,
                    ["newDetailsLogEntry"] = "#[42]. Источник исчерпан.",
                    ["activeEffectDefinitions"] = new JsonArray()
                })
            });
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "quest";
        command["source"]!["sourceId"] = questId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_selector_unresolved");
    }

    [Fact]
    public async Task RawApply_SameTitleQuestsRemainDistinctExactSourcesAfterPatch()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string firstQuestId = "quest_same_title_first";
        const string secondQuestId = "quest_same_title_second";
        var firstQuest = CreateCanonicalQuestSource(firstQuestId, "Active");
        var secondQuest = CreateCanonicalQuestSource(secondQuestId, "Active");
        await context.WriteJsonAsync(
            "game_state/quests/regular_quests.json",
            new JsonObject
            {
                ["quests"] = new JsonArray(
                    firstQuest.DeepClone(),
                    secondQuest.DeepClone())
            });
        await context.WriteJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            "game_state/quests/regular_quests.json",
            new JsonObject
            {
                ["quests"] = new JsonArray(
                    firstQuest.DeepClone(),
                    secondQuest.DeepClone()),
                ["UpdateQuests"] = new JsonArray(new JsonObject
                {
                    ["questId"] = secondQuestId,
                    ["newDetailsLogEntry"] = "#[42]. Второй след уточнён."
                })
            });
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "quest";
        command["source"]!["sourceId"] = firstQuestId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(
            itemIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(backups);

        var normalized = (await context.ReadJsonAsync(
            "game_state/quests/regular_quests.json"))!.AsObject();
        var questIds = normalized["quests"]!.AsArray()
            .OfType<JsonObject>()
            .Select(quest => quest["questId"]!.GetValue<string>())
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { firstQuestId, secondQuestId }, questIds);
    }

    [Fact]
    public async Task RawApply_FactionCorePatchUsesOneComposedExactSource()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string factionId = "faction_effect_source_patch";
        var faction = CreateCanonicalFactionSource(factionId);
        await context.WriteJsonAsync(
            "game_state/factions/faction_core.json",
            new JsonObject { ["factions"] = new JsonArray(faction.DeepClone()) });
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            "game_state/factions/faction_core.json",
            new JsonObject
            {
                ["factions"] = new JsonArray(faction.DeepClone()),
                [FactionCoreChangesContract.PropertyName] = new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["reason"] = "The accepted turn updates an unrelated faction profile group.",
                    ["profile"] = new JsonObject
                    {
                        ["name"] = "Орден точного следа",
                        ["description"] = "Братство сверяет следствия с первичными свидетельствами.",
                        ["image_prompt"] = "weathered archivists beneath a dark stone arch, realistic lighting",
                        ["factionColor"] = "#6B627A"
                    }
                })
            });
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "faction";
        command["source"]!["sourceId"] = factionId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task RawApply_WorldEventCannotForgeSkillSourceKind()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string forgedSkillId = "skill_forged_inside_world_event";
        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(new JsonObject
                {
                    ["eventId"] = "event_real_owner",
                    ["skillId"] = forgedSkillId,
                    ["summary"] = "Над дорогой разнёсся запах крови.",
                    ["isActive"] = true,
                    ["activeEffectDefinitions"] = new JsonArray(
                        EffectMaterializationTestFixture.CreateDefinition())
                })
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "skill";
        command["source"]!["sourceId"] = forgedSkillId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_selector_unresolved");
    }

    [Fact]
    public async Task RawApply_NestedWorldEventPayloadCannotForgeSourceOwner()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string forgedEventId = "event_forged_inside_payload";
        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(new JsonObject
                {
                    ["eventId"] = "event_real_owner",
                    ["summary"] = "Над дорогой разнёсся запах крови.",
                    ["isActive"] = true,
                    ["payload"] = new JsonObject
                    {
                        ["eventId"] = forgedEventId,
                        ["isActive"] = true,
                        ["activeEffectDefinitions"] = new JsonArray(
                            EffectMaterializationTestFixture.CreateDefinition())
                    }
                })
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "world_event";
        command["source"]!["sourceId"] = forgedEventId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_selector_unresolved");
    }

    [Fact]
    public async Task RawApply_WorldEventReferenceAliasCannotForgeSourceOwner()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string forgedEventId = "event_reference_alias_not_owner";
        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(new JsonObject
                {
                    ["worldEventId"] = forgedEventId,
                    ["summary"] = "Ссылка на событие не становится самим событием.",
                    ["isActive"] = true,
                    ["activeEffectDefinitions"] = new JsonArray(
                        EffectMaterializationTestFixture.CreateDefinition())
                })
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "world_event";
        command["source"]!["sourceId"] = forgedEventId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_authority_invalid_owner_identity");
    }

    [Fact]
    public async Task RawApply_NpcFateCardCannotForgeSkillSourceKind()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string forgedSkillId = "skill_forged_on_fate_card";
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_fate_card_owner",
                    ["fateCards"] = new JsonArray(new JsonObject
                    {
                        ["cardId"] = "card_real_owner",
                        ["skillId"] = forgedSkillId,
                        ["isUnlocked"] = true,
                        ["activeEffectDefinitions"] = new JsonArray(
                            EffectMaterializationTestFixture.CreateDefinition())
                    })
                })
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "skill";
        command["source"]!["sourceId"] = forgedSkillId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_selector_unresolved");
    }

    [Fact]
    public async Task RawApply_HazardCannotForgeLocationSourceKind()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string forgedLocationId = "loc_forged_on_hazard";
        var location = new JsonObject
        {
            ["locationId"] = "loc_real_hazard_owner",
            ["hazards"] = new JsonArray(new JsonObject
            {
                ["hazardId"] = "hazard_real_owner",
                ["locationId"] = forgedLocationId,
                ["activeEffectDefinitions"] = new JsonArray(
                    EffectMaterializationTestFixture.CreateDefinition())
            })
        };
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject { ["locations"] = new JsonArray(location) });
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "location";
        command["source"]!["sourceId"] = forgedLocationId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_selector_unresolved");
    }

    [Fact]
    public async Task RawApply_CompletedQuestCannotAuthorizeFreshEffect()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string questId = "quest_completed_effect_source";
        await context.WriteJsonAsync(
            "game_state/quests/regular_quests.json",
            new JsonObject
            {
                ["quests"] = new JsonArray(
                    CreateCanonicalQuestSource(questId, "Completed"))
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "quest";
        command["source"]!["sourceId"] = questId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "effect_source_inactive");
    }

    [Fact]
    public async Task RawApply_HistoricalWorldEventCannotAuthorizeFreshEffect()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string eventId = "event_historical_effect_source";
        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(new JsonObject
                {
                    ["eventId"] = eventId,
                    ["summary"] = "Событие уже осталось только в хронике.",
                    ["activeEffectDefinitions"] = new JsonArray(
                        EffectMaterializationTestFixture.CreateDefinition())
                })
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "world_event";
        command["source"]!["sourceId"] = eventId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "effect_source_inactive");
    }

    [Fact]
    public async Task RawApply_UncarriedItemCannotAuthorizeFreshEffect()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string itemId = "itm_uncarried_effect_source";
        await context.WriteJsonAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["items"] = new JsonArray(new JsonObject
                {
                    ["itemId"] = itemId,
                    ["isCarried"] = false,
                    ["activeEffectDefinitions"] = new JsonArray(
                        EffectMaterializationTestFixture.CreateDefinition())
                })
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "item";
        command["source"]!["sourceId"] = itemId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "effect_source_inactive");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawApply_SourceBoundEquippedItemRequiresExactEquipmentAuthority(
        bool equipped)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string itemId = "itm_equipped_predicate_source";
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "equipped",
            ["onSourceLoss"] = "expire"
        };
        var item = MortalItemTestFixture.CreateCanonicalRoot(itemId);
        item["isCarried"] = true;
        item["activeEffectDefinitions"] = new JsonArray(definition);
        item["materialization"]!["sections"]!["mechanics"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        MortalItemTestFixture.ResealCanonical(item);
        await context.WriteJsonAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(item),
                ["equippedItems"] = equipped
                    ? new JsonObject { ["mainHand"] = itemId }
                    : new JsonObject()
            });
        await context.WriteJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndexForCarrier(
                item,
                "player_inventory",
                "player"));
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "item";
        command["source"]!["sourceId"] = itemId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        if (equipped)
        {
            Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        }
        else
        {
            Assert.Contains(issues, issue =>
                issue.Code == "effect_source_predicate_unsatisfied");
        }
    }

    [Fact]
    public void CanonicalSourceAuthority_PlayerSkillContourSatisfiesUnlockedPredicate()
    {
        const string skillId = "skill_unlocked_predicate_source";
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "unlocked",
            ["onSourceLoss"] = "expire"
        };
        var root = new JsonObject
        {
            ["activeSkillChanges"] = new JsonArray(new JsonObject
            {
                ["skillId"] = skillId,
                ["skillName"] = "Знание кровавого следа",
                ["skillDescription"] = "Изученный навык удерживает связанный эффект.",
                ["rarity"] = "common",
                ["activeEffectDefinitions"] = new JsonArray(definition)
            })
        };

        var authority = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
            {
                ["game_state/player/skills_active.json"] = root
            });
        var resolution = authority.Resolve(
            new EffectSourceKey(
                "mortal_world",
                "skill",
                skillId,
                EffectMaterializationTestFixture.DefinitionKey),
            "player",
            new JsonObject { ["amount"] = 3 });

        Assert.True(
            resolution.Success,
            string.Join(Environment.NewLine, resolution.Issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
    }

    [Fact]
    public void CanonicalSourceAuthority_AfterlifeArtContourSatisfiesUnlockedPredicate()
    {
        var root = JsonNode.Parse(
            AfterlifeEntityProfileValidationTests.BuildValidProfileJson())!.AsObject();
        var profile = root[AfterlifeEntityProfileState.ProfilesProperty]![0]!.AsObject();
        var art = profile["specialArts"]![0]!.AsObject();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["allowedRealms"] = new JsonArray("chaos_sea");
        definition["allowedTargetKinds"] = new JsonArray("guardian");
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "unlocked",
            ["onSourceLoss"] = "expire"
        };
        art["activeEffectDefinitions"] = new JsonArray(definition);

        var authority = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
            {
                [EffectMaterializationTestContext.AfterlifeProfilesPath] = root
            });
        var resolution = authority.Resolve(
            new EffectSourceKey(
                "chaos_sea",
                "spiritual_art",
                art["artId"]!.GetValue<string>(),
                EffectMaterializationTestFixture.DefinitionKey),
            "guardian",
            new JsonObject { ["amount"] = 3 });

        Assert.True(
            resolution.Success,
            string.Join(Environment.NewLine, resolution.Issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
    }

    [Fact]
    public void CanonicalSourceAuthority_QuestActivityUsesOwningCaseInsensitiveStatus()
    {
        const string questId = "quest_uppercase_active_effect_source";
        var root = new JsonObject
        {
            ["quests"] = new JsonArray(new JsonObject
            {
                ["questId"] = questId,
                ["status"] = "ACTIVE",
                ["activeEffectDefinitions"] = new JsonArray(
                    EffectMaterializationTestFixture.CreateDefinition())
            })
        };

        var authority = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
            {
                ["game_state/quests/regular_quests.json"] = root
            });
        var resolution = authority.Resolve(
            new EffectSourceKey(
                "mortal_world",
                "quest",
                questId,
                EffectMaterializationTestFixture.DefinitionKey),
            "player",
            new JsonObject { ["amount"] = 3 });

        Assert.True(
            resolution.Success,
            string.Join(Environment.NewLine, resolution.Issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
    }

    [Fact]
    public void CanonicalSourceAuthority_FateCardUnlockUsesOwningCaseInsensitiveStatus()
    {
        const string cardId = "card_uppercase_unlocked_effect_source";
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["allowedRealms"] = new JsonArray("chaos_sea");
        definition["allowedTargetKinds"] = new JsonArray("guardian");
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "unlocked",
            ["onSourceLoss"] = "expire"
        };
        var root = new JsonObject
        {
            [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(new JsonObject
            {
                ["actorType"] = "guardian",
                ["actorId"] = "guardian_card_effect_source",
                ["realm"] = "Chaos Sea",
                ["fateCards"] = new JsonArray(new JsonObject
                {
                    ["cardId"] = cardId,
                    ["status"] = "Unlocked",
                    ["activeEffectDefinitions"] = new JsonArray(definition)
                })
            })
        };

        var authority = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
            {
                [EffectMaterializationTestContext.AfterlifeProfilesPath] = root
            });
        var resolution = authority.Resolve(
            new EffectSourceKey(
                "chaos_sea",
                "fate_card",
                cardId,
                EffectMaterializationTestFixture.DefinitionKey),
            "guardian",
            new JsonObject { ["amount"] = 3 });

        Assert.True(
            resolution.Success,
            string.Join(Environment.NewLine, resolution.Issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
    }

    [Fact]
    public void CanonicalSourceAuthority_AfterlifeLockedFateCardIgnoresExtraBoolean()
    {
        const string cardId = "card_locked_canonical_effect_source";
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["allowedRealms"] = new JsonArray("chaos_sea");
        definition["allowedTargetKinds"] = new JsonArray("guardian");
        var root = new JsonObject
        {
            [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(new JsonObject
            {
                ["actorType"] = "guardian",
                ["actorId"] = "guardian_locked_card_effect_source",
                ["realm"] = "Chaos Sea",
                ["fateCards"] = new JsonArray(new JsonObject
                {
                    ["cardId"] = cardId,
                    ["status"] = "locked",
                    ["isUnlocked"] = true,
                    ["activeEffectDefinitions"] = new JsonArray(definition)
                })
            })
        };
        var authority = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
            {
                [EffectMaterializationTestContext.AfterlifeProfilesPath] = root
            });

        var resolution = authority.Resolve(
            new EffectSourceKey(
                "chaos_sea",
                "fate_card",
                cardId,
                EffectMaterializationTestFixture.DefinitionKey),
            "guardian",
            new JsonObject { ["amount"] = 3 });

        Assert.False(resolution.Success);
        Assert.Contains(resolution.Issues, issue => issue.Code == "effect_source_inactive");
    }

    [Fact]
    public async Task RawApply_SameTurnExistingItemPlacementUsesComposedNonPlayerState()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string itemId = "itm_moved_out_of_backpack_effect_source";
        var item = MortalItemTestFixture.CreateCanonicalRoot(itemId);
        item["isCarried"] = true;
        item["activeEffectDefinitions"] = new JsonArray(
            EffectMaterializationTestFixture.CreateDefinition());
        item["materialization"]!["sections"]!["mechanics"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        MortalItemTestFixture.ResealCanonical(item);
        await context.WriteJsonAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject { ["items"] = new JsonArray(item.DeepClone()) });
        await context.WriteJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndexForCarrier(
                item,
                "player_inventory",
                "player"));
        await context.CaptureValidatedPendingSnapshotAsync();
        var movedItem = item.DeepClone().AsObject();
        movedItem["isCarried"] = false;
        await context.WriteJsonAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject { ["items"] = new JsonArray(movedItem) });
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "item";
        command["source"]!["sourceId"] = itemId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_selector_unresolved");
    }

    [Fact]
    public async Task RawApply_DoesNotTrustUnvalidatedSameTurnSourceSibling()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string sourceId = "event_unvalidated_effect_source";
        const string path = "game_state/world/world_events.json";
        await context.WriteJsonAsync(
            path,
            new JsonObject { ["worldEventsLog"] = new JsonArray() });
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            path,
            new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["eventId"] = sourceId,
                    ["activeEffectDefinitions"] = new JsonArray(
                        EffectMaterializationTestFixture.CreateDefinition())
                })
            });
        var command = CreateMaterializableApplyCommand();
        command["source"]!["kind"] = "world_event";
        command["source"]!["sourceId"] = sourceId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Contains(issues, issue =>
            issue.Code is "missing_allowed_top_level_key" or
                "effect_source_selector_unresolved");
    }

    [Fact]
    public async Task RawApply_RejectsMalformedDefinitionAtOwningSourceBoundary()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition.Remove("removal");
        await context.SeedPlayerSkillSourceAsync(definition);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateMaterializableApplyCommand()));

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "effect_source_definition_missing_field");
    }

    [Fact]
    public async Task RawApply_ComposesNewExactFactionSourceAndNpcTargetAfterSnapshot()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string factionInitialId = "temp-faction-effect-source";
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
        await context.WriteJsonAsync(
            "game_state/factions/faction_core.json",
            new JsonObject
            {
                ["factionDataChanges"] = new JsonArray(
                    EffectMaterializationTestFixture.CreateSameTurnMortalFaction(
                        factionInitialId))
            });
        var sameTurnNpc = EffectMaterializationTestFixture.CreateSameTurnMortalActor(
            "npcref_test_healer");
        sameTurnNpc["resourceMaterialization"] = CreateResourceMaterialization(
            ("health", 60m));
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(sameTurnNpc)
            });
        var command = CreateMaterializableApplyCommand("npc");
        command["target"] = new JsonObject
        {
            ["kind"] = "npc",
            ["targetRef"] = "npcref_test_healer"
        };
        command["source"]!["kind"] = "faction";
        command["source"]!["sourceId"] = factionInitialId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
    }

    [Fact]
    public async Task RawApply_NewNpcDeltaPreservesExistingStableNpcTargetAuthority()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string existingNpcId = "npc_existing_effect_target";
        const string newNpcRef = "npcref_new_effect_target";
        await context.SeedPlayerSkillSourceAsync();
        var existingNpc = MortalActorTestFixtures.CreateActor(existingNpcId);
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(existingNpc.DeepClone())
            });
        var canonicalLocation = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            MortalActorTestFixtures.DefaultLocationId,
            "Validated effect target fixture location");
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(canonicalLocation));
        await context.CaptureValidatedPendingSnapshotAsync();
        var sameTurnNpc = EffectMaterializationTestFixture.CreateSameTurnMortalActor(
            newNpcRef);
        sameTurnNpc["resourceMaterialization"] = CreateResourceMaterialization(
            ("health", 60m));
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(existingNpc.DeepClone()),
                ["UpdateNPCs"] = new JsonArray(sameTurnNpc)
            });
        var command = CreateMaterializableApplyCommand("npc");
        command["target"] = new JsonObject
        {
            ["kind"] = "npc",
            ["targetId"] = existingNpcId
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task RawApply_ComposesLocationSourceRefIntoPermanentSourceIdentity()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        await context.SeedMortalPlayerResourcesAsync();
        context.FileSystem.DeleteFile(
            MortalLocationMaterializationContract.CurrentLocationPath);
        await context.CaptureValidatedPendingSnapshotAsync();
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["activeEffectDefinitions"] = new JsonArray(
            EffectMaterializationTestFixture.CreateDefinition());
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["worldMapUpdates"] = new JsonObject
                {
                    ["newLocations"] = new JsonArray(location),
                    ["newLinks"] = new JsonArray()
                }
            });
        var command = CreateMaterializableApplyCommand();
        command["source"] = new JsonObject
        {
            ["kind"] = "location",
            ["sourceRef"] = MortalLocationTestFixture.LocationInitialId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var locationIssues =
            await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();
        var itemIssues =
            await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var effectIssues =
            await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(locationIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(
            effectIssues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, effectIssues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));

        var postIssues = await context.NormalizeAcceptedTurnWithIssuesAsync();
        Assert.True(
            postIssues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, postIssues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        var map = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
        var canonicalLocation = map["locations"]!.AsArray()
            .OfType<JsonObject>()
            .Single(candidate => string.Equals(
                candidate["materializationReceipt"]?["initialId"]?.GetValue<string>(),
                MortalLocationTestFixture.LocationInitialId,
                StringComparison.Ordinal));
        var effectCarrier = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        var effect = Assert.IsType<JsonObject>(
            Assert.Single(effectCarrier["activeEffects"]!.AsArray()));
        Assert.Equal(
            canonicalLocation["locationId"]!.GetValue<string>(),
            effect["source"]!["sourceId"]!.GetValue<string>());
        Assert.Null(effect["source"]!["sourceRef"]);
    }

    [Fact]
    public async Task RawApply_ComposesSameTurnNewLocationHazardSource()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        const string hazardId = "hazard_same_turn_effect_source";
        await context.BuildMortalBootstrapAsync();
        await context.SeedMortalPlayerResourcesAsync();
        context.FileSystem.DeleteFile(
            MortalLocationMaterializationContract.CurrentLocationPath);
        await context.CaptureValidatedPendingSnapshotAsync();
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["hazards"] = new JsonArray(new JsonObject
        {
            ["hazardId"] = hazardId,
            ["status"] = "active",
            ["activeEffectDefinitions"] = new JsonArray(
                EffectMaterializationTestFixture.CreateDefinition())
        });
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["worldMapUpdates"] = new JsonObject
                {
                    ["newLocations"] = new JsonArray(location),
                    ["newLinks"] = new JsonArray()
                }
            });
        var command = CreateMaterializableApplyCommand();
        command["source"] = new JsonObject
        {
            ["kind"] = "hazard",
            ["sourceId"] = hazardId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var locationIssues =
            await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();
        var itemIssues =
            await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var effectIssues =
            await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(locationIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(
            effectIssues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, effectIssues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));

        var postIssues = await context.NormalizeAcceptedTurnWithIssuesAsync();
        Assert.True(
            postIssues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, postIssues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        var effectCarrier = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        var effect = Assert.IsType<JsonObject>(
            Assert.Single(effectCarrier["activeEffects"]!.AsArray()));
        Assert.Equal(hazardId, effect["source"]!["sourceId"]!.GetValue<string>());
        Assert.Null(effect["source"]!["sourceRef"]);
    }

    [Fact]
    public async Task RawApply_ComposesItemCreationRefIntoPermanentSourceIdentity()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var rawItem = MortalItemTestFixture.CreateRawRoot();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "carried",
            ["onSourceLoss"] = "expire"
        };
        rawItem["activeEffectDefinitions"] = new JsonArray(definition);
        rawItem["materialization"]!["sections"]!["mechanics"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        var arrangement = await context.ArrangeRouteAsync(
            "player_acquisition",
            "turn_outcome",
            rawItem,
            includeMortalPlayerResources: true);
        var command = CreateMaterializableApplyCommand();
        command["source"] = new JsonObject
        {
            ["kind"] = "item",
            ["sourceRef"] = arrangement.CreationRef,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var itemIssues =
            await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var effectIssues =
            await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(
            effectIssues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, effectIssues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));

        var postIssues = await context.NormalizeAcceptedTurnWithIssuesAsync();
        Assert.True(
            postIssues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, postIssues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        var items = (await context.ReadJsonAsync(
            InventoryEquipmentService.ItemsPath))!.AsObject();
        var canonicalItem = items["items"]!.AsArray()
            .OfType<JsonObject>()
            .Single(item => string.Equals(
                item["materializationReceipt"]?["creationRef"]?.GetValue<string>(),
                arrangement.CreationRef,
                StringComparison.Ordinal));
        var effectCarrier = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        var effect = Assert.IsType<JsonObject>(
            Assert.Single(effectCarrier["activeEffects"]!.AsArray()));
        Assert.Equal(
            canonicalItem["itemId"]!.GetValue<string>(),
            effect["source"]!["sourceId"]!.GetValue<string>());
        Assert.Null(effect["source"]!["sourceRef"]);
    }

    [Fact]
    public async Task RawApply_FailedItemRevalidationInvalidatesAcceptedItemSourceHandoff()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var rawItem = MortalItemTestFixture.CreateRawRoot();
        rawItem["activeEffectDefinitions"] = new JsonArray(
            EffectMaterializationTestFixture.CreateDefinition());
        rawItem["materialization"]!["sections"]!["mechanics"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        var arrangement = await context.ArrangeRouteAsync(
            "player_acquisition",
            "turn_outcome",
            rawItem);
        var command = CreateMaterializableApplyCommand();
        command["source"] = new JsonObject
        {
            ["kind"] = "item",
            ["sourceRef"] = arrangement.CreationRef,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var firstItemIssues =
            await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var firstEffectIssues =
            await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();
        Assert.DoesNotContain(firstItemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(firstEffectIssues, issue => issue.Severity == IssueSeverity.Error);

        var items = (await context.ReadJsonAsync(
            InventoryEquipmentService.ItemsPath))!.AsObject();
        items["UpdateInventory"]![0]!["itemId"] = "itm_forged_after_validation";
        await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath, items);

        var failedItemIssues =
            await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var secondEffectIssues =
            await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(failedItemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Contains(secondEffectIssues, issue =>
            issue.Code == "effect_source_selector_unresolved");
    }

    [Theory]
    [InlineData("npc_acquisition", "npc_inventory_add")]
    [InlineData("storage_placement", "location_storage")]
    public async Task RawApply_NewNonPlayerItemCannotAuthorizeFreshPlayerEffect(
        string route,
        string authorityKind)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var rawItem = MortalItemTestFixture.CreateRawRoot();
        rawItem["activeEffectDefinitions"] = new JsonArray(
            EffectMaterializationTestFixture.CreateDefinition());
        rawItem["materialization"]!["sections"]!["mechanics"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        var arrangement = await context.ArrangeRouteAsync(
            route,
            authorityKind,
            rawItem);
        var command = CreateMaterializableApplyCommand();
        command["source"] = new JsonObject
        {
            ["kind"] = "item",
            ["sourceRef"] = arrangement.CreationRef,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var itemIssues =
            await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var effectIssues =
            await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Contains(effectIssues, issue =>
            issue.Code == "effect_source_selector_unresolved");
    }

    [Fact]
    public async Task RawApply_NewNamedCombatRepresentationRejectsUnknownNpcRef()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerSkillSourceAsync();
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject { ["NPCsInScene"] = new JsonArray() });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray() });
        await context.CaptureValidatedPendingSnapshotAsync();

        var combatant = CreateMaterializedCombatant(
            "combatant_ref_unknown_named_npc");
        combatant.Remove("resourceMaterialization");
        combatant.Remove("combatantRef");
        combatant["npcRef"] = "npcref_missing_combatant_binding";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(combatant)
            });
        var command = CreateMaterializableApplyCommand("npc");
        command["target"] = new JsonObject
        {
            ["kind"] = "npc",
            ["targetRef"] = "npcref_missing_combatant_binding"
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_combat_npc_ref_unresolved");
    }

    [Fact]
    public async Task CanonicalCombatant_RejectsUnknownNpcBinding()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var combatant = EffectMaterializationTestFixture.CreateSameTurnCombatant(
            "combatant_ref_canonical_unknown_npc");
        combatant.Remove("combatantRef");
        combatant["combatantId"] = "combatant_canonical_unknown_npc";
        combatant["NPCId"] = "npc_missing_canonical_binding";
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject { ["NPCsInScene"] = new JsonArray() });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(combatant)
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_target_combatant_npc_binding_unresolved");
    }

    [Fact]
    public async Task RawApply_NewNamedCombatRepresentationBindsSameTurnNpcAndEffectTarget()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string npcInitialId = "npcref_named_combatant_healer";
        await context.SeedPlayerSkillSourceAsync();
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject { ["NPCsInScene"] = new JsonArray() });
        var canonicalLocation = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            MortalActorTestFixtures.DefaultLocationId,
            "Named combatant binding fixture location");
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(canonicalLocation));
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray() });
        await context.WriteJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var sameTurnNpc = EffectMaterializationTestFixture.CreateSameTurnMortalActor(
            npcInitialId);
        sameTurnNpc["resourceMaterialization"] = CreateResourceMaterialization(
            ("health", 60m));
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(sameTurnNpc)
            });
        var combatant = CreateMaterializedCombatant(
            "combatant_ref_named_healer");
        combatant.Remove("resourceMaterialization");
        combatant.Remove("combatantRef");
        combatant["npcRef"] = npcInitialId;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(combatant)
            });
        var command = CreateMaterializableApplyCommand("npc");
        command["target"] = new JsonObject
        {
            ["kind"] = "npc",
            ["targetRef"] = npcInitialId
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(
            itemIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var rawIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            rawIssues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, rawIssues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));

        await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(backups);

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var canonicalCombatant = Assert.IsType<JsonObject>(
            Assert.Single(root["enemiesData"]!.AsArray()));
        Assert.Equal(npcInitialId, canonicalCombatant["NPCId"]!.GetValue<string>());
        Assert.False(canonicalCombatant.ContainsKey("npcRef"));
        Assert.False(canonicalCombatant.ContainsKey("combatantId"));
        Assert.False(canonicalCombatant.ContainsKey("combatantRef"));
        Assert.Empty(canonicalCombatant["activeDebuffs"]!.AsArray());

        var npcEffects = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath))!.AsObject();
        var npcCarrier = Assert.IsType<JsonObject>(
            Assert.Single(npcEffects["entries"]!.AsArray()));
        Assert.Equal(npcInitialId, npcCarrier["NPCId"]!.GetValue<string>());
        Assert.Single(npcCarrier["activeEffects"]!.AsArray());
    }

    [Fact]
    public async Task RawApply_ComposesCombatantRefIntoOnePermanentTargetAndCarrier()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerSkillSourceAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(
                    CreateMaterializedCombatant(
                        EffectMaterializationTestFixture.CombatantRef))
            });
        var command = CreateMaterializableApplyCommand("combatant");
        command["target"] = new JsonObject
        {
            ["kind"] = "combatant",
            ["targetRef"] = EffectMaterializationTestFixture.CombatantRef
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));

        await context.NormalizeAcceptedEffectsAsync(backups);

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var combatant = Assert.IsType<JsonObject>(Assert.Single(root["enemiesData"]!.AsArray()));
        var combatantId = combatant["combatantId"]!.GetValue<string>();
        Assert.StartsWith("combatant_", combatantId, StringComparison.Ordinal);
        Assert.Null(combatant["combatantRef"]);
        Assert.Equal(17, combatant["initiative"]!.GetValue<int>());
        var effect = Assert.IsType<JsonObject>(Assert.Single(combatant["activeDebuffs"]!.AsArray()));
        Assert.Equal(combatantId, effect["target"]!["targetId"]!.GetValue<string>());

        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()));
        Assert.Equal(combatantId, entry["target"]!["targetId"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(EffectMaterializationTestContext.CommandPath));
    }

    [Fact]
    public async Task RawApply_DetachedGroupMemberKeepsMemberTargetAndCarrier()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerSkillSourceAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray() });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        var createBackups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(
                    CreateMaterializedCombatGroup(
                        ("member_ref_effect_scout", "Разведчик"),
                        ("member_ref_effect_archer", "Лучник")))
            });
        var createIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(createIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.NotNull(await context.NormalizeAcceptedEffectsAsync(createBackups));

        var canonicalRoot = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var canonicalGroup = Assert.IsType<JsonObject>(
            Assert.Single(canonicalRoot["enemiesData"]!.AsArray()));
        var members = canonicalGroup["members"]!.AsArray()
            .Select(node => Assert.IsType<JsonObject>(node))
            .ToArray();
        var detachedMember = members[0].DeepClone().AsObject();
        var retainedMember = members[1].DeepClone().AsObject();
        var memberId = detachedMember["memberId"]!.GetValue<string>();

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var splitBackups = await context.ReadPendingSnapshotBackupsAsync();
        var splitGroup = canonicalGroup.DeepClone().AsObject();
        splitGroup["count"] = 1;
        splitGroup["members"] = new JsonArray(retainedMember);
        var detachedRow = CreateMaterializedCombatant("unused_detached_ref");
        detachedRow.Remove("combatantRef");
        detachedRow.Remove("resourceMaterialization");
        detachedRow["memberId"] = memberId;
        detachedRow["name"] = "Оглушённый разведчик";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(splitGroup, detachedRow)
            });
        var command = CreateMaterializableApplyCommand("combatant");
        command["eventRef"]!["authorityId"] = "turn_43";
        command["target"] = new JsonObject
        {
            ["kind"] = "combatant",
            ["targetId"] = memberId
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        Assert.NotNull(await context.NormalizeAcceptedEffectsAsync(splitBackups));

        var publishedRoot = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var publishedRows = publishedRoot["enemiesData"]!.AsArray()
            .Select(node => Assert.IsType<JsonObject>(node))
            .ToArray();
        var publishedDetached = Assert.Single(publishedRows, row =>
            row["memberId"]?.GetValue<string>() == memberId);
        Assert.False(publishedDetached.ContainsKey("combatantId"));
        var effect = Assert.IsType<JsonObject>(
            Assert.Single(publishedDetached["activeDebuffs"]!.AsArray()));
        Assert.Equal("combatant", effect["target"]!["kind"]!.GetValue<string>());
        Assert.Equal(memberId, effect["target"]!["targetId"]!.GetValue<string>());
        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()));
        Assert.Equal(memberId, entry["target"]!["targetId"]!.GetValue<string>());

        await context.CaptureValidatedPendingSnapshotAsync(turn: 44);
        var rejoinBackups = await context.ReadPendingSnapshotBackupsAsync();
        var publishedGroup = Assert.Single(publishedRows, row =>
            row["isGroup"]?.GetValue<bool>() == true).DeepClone().AsObject();
        publishedGroup["count"] = 2;
        publishedGroup["members"] = new JsonArray(
            retainedMember.DeepClone(),
            publishedDetached.DeepClone());
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(publishedGroup)
            });

        var rejoinIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            rejoinIssues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, rejoinIssues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        Assert.NotNull(await context.NormalizeAcceptedEffectsAsync(rejoinBackups));
        var rejoinedRoot = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var rejoinedGroup = Assert.IsType<JsonObject>(
            Assert.Single(rejoinedRoot["enemiesData"]!.AsArray()));
        var rejoinedMember = Assert.Single(
            rejoinedGroup["members"]!.AsArray().OfType<JsonObject>(),
            member => member["memberId"]?.GetValue<string>() == memberId);
        var rejoinedEffect = Assert.IsType<JsonObject>(
            Assert.Single(rejoinedMember["activeDebuffs"]!.AsArray()));
        Assert.Equal(memberId, rejoinedEffect["target"]!["targetId"]!.GetValue<string>());
        var rejoinedIndex = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var rejoinedEntry = Assert.IsType<JsonObject>(
            Assert.Single(rejoinedIndex["entries"]!.AsArray()));
        Assert.Equal(memberId, rejoinedEntry["target"]!["targetId"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawApply_NewMemberRefBecomesOnePermanentMemberTargetAndCarrier(
        bool nestedInGroup)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerSkillSourceAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray() });
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        const string memberRef = "member_ref_detached_same_turn";
        var detached = CreateMaterializedCombatant("unused_combatant_ref");
        detached.Remove("combatantRef");
        detached["memberRef"] = memberRef;
        var rawOwner = nestedInGroup
            ? CreateMaterializedCombatGroup((memberRef, "Разведчик"))
            : detached;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray(rawOwner) });
        var command = CreateMaterializableApplyCommand("combatant");
        command["target"] = new JsonObject
        {
            ["kind"] = "combatant",
            ["targetRef"] = memberRef
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        Assert.NotNull(await context.NormalizeAcceptedEffectsAsync(backups));

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var publishedRow = Assert.IsType<JsonObject>(
            Assert.Single(root["enemiesData"]!.AsArray()));
        var published = nestedInGroup
            ? Assert.IsType<JsonObject>(
                Assert.Single(publishedRow["members"]!.AsArray()))
            : publishedRow;
        var memberId = published["memberId"]!.GetValue<string>();
        Assert.StartsWith("member_", memberId, StringComparison.Ordinal);
        Assert.False(published.ContainsKey("memberRef"));
        Assert.False(published.ContainsKey("combatantId"));
        Assert.False(published.ContainsKey("combatantRef"));
        var effect = Assert.IsType<JsonObject>(
            Assert.Single(published["activeDebuffs"]!.AsArray()));
        Assert.Equal(memberId, effect["target"]!["targetId"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("combat_identity_shared", "combat_identity_shared", "effect_target_authority_duplicate_target")]
    [InlineData("combat_identity_shared", "COMBAT_IDENTITY_SHARED", "effect_target_authority_confusable_target")]
    public void CanonicalTargets_RejectCombatantAndDetachedMemberIdentityAmbiguity(
        string combatantId,
        string memberId,
        string expectedCode)
    {
        var combatant = CreateMaterializedCombatant("unused_combatant_ref");
        combatant.Remove("combatantRef");
        combatant.Remove("resourceMaterialization");
        combatant["combatantId"] = combatantId;
        var detachedMember = CreateMaterializedCombatant("unused_member_ref");
        detachedMember.Remove("combatantRef");
        detachedMember.Remove("resourceMaterialization");
        detachedMember["memberId"] = memberId;
        var authority = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(
            new EffectCarrierCatalogInput(
                null,
                null,
                new JsonObject
                {
                    ["enemiesData"] = new JsonArray(combatant, detachedMember)
                },
                null,
                null,
                null),
            new Dictionary<string, JsonNode?>());

        Assert.Contains(authority.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public async Task RawApply_ConsumesEveryAcceptedCombatantRefWhenOneIsTargeted()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerSkillSourceAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray() });
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var targeted = CreateMaterializedCombatant(
            "combatant_ref_targeted_raider");
        var sibling = CreateMaterializedCombatant(
            "combatant_ref_unreferenced_scout");
        sibling["name"] = "Разведчик";
        sibling["initiative"] = 11;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(targeted, sibling)
            });
        var command = CreateMaterializableApplyCommand("combatant");
        command["target"] = new JsonObject
        {
            ["kind"] = "combatant",
            ["targetRef"] = "combatant_ref_targeted_raider"
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        await context.NormalizeAcceptedEffectsAsync(backups);

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var combatants = root["enemiesData"]!.AsArray()
            .Select(node => Assert.IsType<JsonObject>(node))
            .ToArray();
        Assert.Equal(2, combatants.Length);
        Assert.All(combatants, combatant =>
        {
            Assert.StartsWith(
                "combatant_",
                combatant["combatantId"]!.GetValue<string>(),
                StringComparison.Ordinal);
            Assert.Null(combatant["combatantRef"]);
        });
        Assert.NotEqual(
            combatants[0]["combatantId"]!.GetValue<string>(),
            combatants[1]["combatantId"]!.GetValue<string>());
        Assert.Single(combatants[0]["activeDebuffs"]!.AsArray());
        Assert.Empty(combatants[1]["activeDebuffs"]!.AsArray());
        Assert.Equal(11, combatants[1]["initiative"]!.GetValue<int>());
    }

    [Fact]
    public async Task RawCombatantTurn_ConsumesCombatantRefWithoutEffectCommand()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray() });
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(
                    CreateMaterializedCombatant(
                        "combatant_ref_commandless_raider"))
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        var plan = await context.NormalizeAcceptedEffectsAsync(backups);

        Assert.NotNull(plan);
        Assert.Contains(EffectAcceptedTurnPlan.IdentityIndexPath, plan.TouchedPaths);
        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var combatant = Assert.IsType<JsonObject>(Assert.Single(root["enemiesData"]!.AsArray()));
        Assert.StartsWith(
            "combatant_",
            combatant["combatantId"]!.GetValue<string>(),
            StringComparison.Ordinal);
        Assert.Null(combatant["combatantRef"]);
        Assert.Empty(combatant["activeBuffs"]!.AsArray());
        Assert.Empty(combatant["activeDebuffs"]!.AsArray());
    }

    [Fact]
    public async Task RawCombatantTurn_CommandlessPlanTracksAcceptedSourceOwnerChange()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            new JsonArray());
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray() });
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.SeedPlayerSkillSourceAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(
                    CreateMaterializedCombatant(
                        "combatant_ref_commandless_with_source_change"))
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code} {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        var plan = await context.NormalizeAcceptedEffectsAsync(backups);

        Assert.NotNull(plan);
        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var combatant = Assert.IsType<JsonObject>(Assert.Single(root["enemiesData"]!.AsArray()));
        Assert.StartsWith(
            "combatant_",
            combatant["combatantId"]!.GetValue<string>(),
            StringComparison.Ordinal);
        Assert.Null(combatant["combatantRef"]);
    }

    [Fact]
    public async Task RawSourceOwner_UnresolvedDefinitionLinkFailsWithoutEffectCommand()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            new JsonArray());
        await context.CaptureValidatedPendingSnapshotAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["links"] = new JsonArray(new JsonObject
        {
            ["kind"] = "quest",
            ["targetId"] = "quest_missing_effect_link",
            ["role"] = "context"
        });
        await context.SeedPlayerSkillSourceAsync(definition);

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_link_target_unresolved");
    }

    private static JsonNode CreateEmptySameTurnOwnerRoot(string path) =>
        path switch
        {
            "game_state/player/wounds.json" => new JsonArray(),
            "game_state/quests/regular_quests.json" =>
                new JsonObject { ["quests"] = new JsonArray() },
            "game_state/world/world_events.json" =>
                new JsonObject { ["worldEventsLog"] = new JsonArray() },
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
        };

    private static JsonNode CreateCompleteSameTurnOwnerRoot(
        string path,
        string sourceId) =>
        path switch
        {
            "game_state/player/wounds.json" => new JsonArray(new JsonObject
            {
                ["woundId"] = sourceId,
                ["woundName"] = "Свежая рана",
                ["severity"] = "severe",
                ["description"] = "Рана требует немедленного лечения.",
                ["activeEffectDefinitions"] = new JsonArray(
                    EffectMaterializationTestFixture.CreateDefinition())
            }),
            "game_state/quests/regular_quests.json" => new JsonObject
            {
                ["UpdateQuests"] = new JsonArray(new JsonObject
                {
                    ["questId"] = null,
                    ["initialId"] = sourceId,
                    ["questName"] = "След кровотечения",
                    ["status"] = "Active",
                    ["questGiver"] = "Полевой лекарь",
                    ["questBackground"] = "Рана открыла след к нападавшему.",
                    ["description"] = "Найти источник опасного следа.",
                    ["objectives"] = new JsonArray(),
                    ["detailsLog"] = new JsonArray("#[42]. След обнаружен."),
                    ["activeEffectDefinitions"] = new JsonArray(
                        EffectMaterializationTestFixture.CreateDefinition())
                })
            },
            "game_state/world/world_events.json" => new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(new JsonObject
                {
                    ["eventId"] = sourceId,
                    ["summary"] = "Над дорогой разнёсся запах крови.",
                    ["isActive"] = true,
                    ["activeEffectDefinitions"] = new JsonArray(
                        EffectMaterializationTestFixture.CreateDefinition())
                })
            },
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
        };

    private static JsonObject CreateCanonicalQuestSource(
        string questId,
        string status) =>
        new()
        {
            ["questId"] = questId,
            ["questName"] = "След кровотечения",
            ["status"] = status,
            ["questGiver"] = "Полевой лекарь",
            ["questBackground"] = "Рана открыла след к нападавшему.",
            ["description"] = "Найти источник опасного следа.",
            ["objectives"] = new JsonArray(),
            ["detailsLog"] = new JsonArray("#[41]. След обнаружен."),
            ["activeEffectDefinitions"] = new JsonArray(
                EffectMaterializationTestFixture.CreateDefinition())
        };

    private static JsonObject CreateCanonicalFactionSource(string factionId)
    {
        var faction = EffectMaterializationTestFixture
            .CreateSameTurnMortalFaction(factionId);
        faction["factionId"] = factionId;
        faction.Remove("initialId");
        faction.Remove("isNewFaction");
        faction["materialization"]!["factionId"] = factionId;
        return faction;
    }

    private static JsonObject CreateValidActiveSkill(
        string skillId,
        string skillName,
        JsonObject? definition = null)
    {
        var skill = new JsonObject
        {
            ["skillId"] = skillId,
            ["skillName"] = skillName,
            ["skillDescription"] = "Полная форма активного навыка для проверки композиции.",
            ["rarity"] = "common",
            ["actionCost"] = "Main",
            ["combatEffect"] = new JsonObject
            {
                ["isActivatedEffect"] = true,
                ["actionName"] = skillName,
                ["actionCost"] = "Main",
                ["effects"] = new JsonArray(new JsonObject
                {
                    ["effectType"] = "Damage",
                    ["value"] = "10%",
                    ["targetType"] = "enemy",
                    ["effectDescription"] = "Навык наносит проверочный урон.",
                    ["poiseDamage"] = "5%"
                })
            }
        };
        if (definition != null)
            skill["activeEffectDefinitions"] = new JsonArray(definition);
        return skill;
    }

    private static JsonObject CreateMaterializedCombatant(string combatantRef)
    {
        var combatant = EffectMaterializationTestFixture.CreateSameTurnCombatant(
            combatantRef);
        combatant.Remove("currentHealth");
        combatant.Remove("maxHealth");
        combatant.Remove("currentPoise");
        combatant.Remove("maxPoise");
        combatant["resourceMaterialization"] = CreateResourceMaterialization(
            ("health", 100m),
            ("poise", 100m));
        return combatant;
    }

    private static JsonObject CreateMaterializedCombatGroup(
        params (string MemberRef, string Name)[] members) =>
        new()
        {
            ["NPCId"] = null,
            ["name"] = "Дозор",
            ["image_prompt"] = "dark fantasy road watch",
            ["description"] = "Малый дорожный дозор.",
            ["type"] = "group",
            ["isGroup"] = true,
            ["initiative"] = 12,
            ["actions"] = new JsonArray(),
            ["resistances"] = new JsonArray(),
            ["activeBuffs"] = new JsonArray(),
            ["activeDebuffs"] = new JsonArray(),
            ["count"] = members.Length,
            ["unitName"] = "дозорный",
            ["members"] = new JsonArray(members.Select(member => (JsonNode)new JsonObject
            {
                ["memberRef"] = member.MemberRef,
                ["name"] = member.Name,
                ["resourceMaterialization"] = CreateResourceMaterialization(
                    ("health", 30m),
                    ("poise", 20m))
            }).ToArray())
        };

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

    private static JsonNode CreateCanonicalSourceRoot(
        string path,
        JsonObject source) =>
        path switch
        {
            "game_state/player/skills_active.json" => new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(source)
            },
            "game_state/inventory/items.json" => new JsonObject
            {
                ["items"] = new JsonArray(source)
            },
            "game_state/quests/regular_quests.json" => new JsonObject
            {
                ["quests"] = new JsonArray(source)
            },
            MortalLocationMaterializationContract.WorldMapPath => new JsonObject
            {
                ["locations"] = new JsonArray(source)
            },
            "game_state/factions/faction_core.json" => new JsonObject
            {
                ["factions"] = new JsonArray(source)
            },
            "game_state/world/world_events.json" => new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(source)
            },
            "game_state/npcs/npc_core.json" => new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_effect_source_owner",
                    ["actions"] = new JsonArray(source)
                })
            },
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
        };
}
