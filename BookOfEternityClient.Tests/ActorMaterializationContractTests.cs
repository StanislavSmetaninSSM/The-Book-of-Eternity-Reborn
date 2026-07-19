using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ActorMaterializationContractTests
{
    [Fact]
    public void Validate_ValidMortalEnvelope_ReturnsNoIssues()
    {
        using var document = JsonDocument.Parse("""
        {
          "materialization": {
            "schemaVersion": 1,
            "materializationId": "mat_npc_arden_turn_1",
            "actorType": "mortal_npc",
            "actorId": "npc_arden",
            "materializedAtTurn": 1,
            "state": "complete",
            "capabilities": {
              "canFight": true,
              "canTeach": false,
              "canTrade": false,
              "ownsItems": true
            },
            "sections": {
              "skills": { "state": "populated" },
              "inventory": { "state": "populated" },
              "fateCards": {
                "state": "empty_by_design",
                "reason": "Его судьба пока не открыла отдельной карты."
              },
              "personalQuests": { "state": "populated" },
              "relationships": { "state": "populated" }
            }
          }
        }
        """);

        var evidence = new ActorMaterializationEvidence(
            ActorType: "mortal_npc",
            ActorId: "npc_arden",
            SectionHasContent: new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["skills"] = true,
                ["inventory"] = true,
                ["fateCards"] = false,
                ["personalQuests"] = true,
                ["relationships"] = true
            },
            CapabilityEvidence: new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["canFight"] = true,
                ["canTeach"] = false,
                ["canTrade"] = false,
                ["ownsItems"] = true
            });

        var issues = ActorMaterializationContract.Validate(
            document.RootElement,
            "npc",
            ActorMaterializationFamily.Mortal,
            evidence,
            requireEnvelope: true);

        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_RequiredEnvelopeMissing_ReturnsMissingIssue()
    {
        using var document = JsonDocument.Parse("{}");

        var issues = ActorMaterializationContract.Validate(
            document.RootElement,
            "npc",
            ActorMaterializationFamily.Mortal,
            CreateMortalEvidence(),
            requireEnvelope: true);

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_missing");
    }

    [Fact]
    public void Validate_OptionalLegacyEnvelopeMissing_ReturnsNoIssues()
    {
        using var document = JsonDocument.Parse("{}");

        var issues = ActorMaterializationContract.Validate(
            document.RootElement,
            "npc",
            ActorMaterializationFamily.Mortal,
            CreateMortalEvidence(),
            requireEnvelope: false);

        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("actorType", "guardian")]
    [InlineData("actorId", "npc_someone_else")]
    public void Validate_EnvelopeBindingDoesNotMatchActor_ReturnsBindingIssue(string propertyName, string propertyValue)
    {
        var envelope = CreateMortalEnvelope();
        envelope = envelope.Replace($"\"{propertyName}\": \"{(propertyName == "actorType" ? "mortal_npc" : "npc_arden")}\"", $"\"{propertyName}\": \"{propertyValue}\"");
        using var document = JsonDocument.Parse(envelope);

        var issues = ActorMaterializationContract.Validate(
            document.RootElement,
            "npc",
            ActorMaterializationFamily.Mortal,
            CreateMortalEvidence(),
            requireEnvelope: true);

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_actor_binding_mismatch");
    }

    [Fact]
    public void Validate_UnknownEnvelopeField_ReturnsInvalidEnvelopeIssue()
    {
        var envelope = CreateMortalEnvelope().Replace("\"state\": \"complete\",", "\"state\": \"complete\", \"internalGuess\": true,");
        using var document = JsonDocument.Parse(envelope);

        var issues = ActorMaterializationContract.Validate(
            document.RootElement,
            "npc",
            ActorMaterializationFamily.Mortal,
            CreateMortalEvidence(),
            requireEnvelope: true);

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_invalid_envelope");
    }

    [Fact]
    public void Validate_EmptyByDesignWithoutReason_ReturnsInvalidEnvelopeIssue()
    {
        var envelope = CreateMortalEnvelope().Replace(
            "\"fateCards\": { \"state\": \"empty_by_design\", \"reason\": \"Его судьба пока не открыла отдельной карты.\" }",
            "\"fateCards\": { \"state\": \"empty_by_design\" }");
        using var document = JsonDocument.Parse(envelope);

        var issues = ActorMaterializationContract.Validate(
            document.RootElement,
            "npc",
            ActorMaterializationFamily.Mortal,
            CreateMortalEvidence(),
            requireEnvelope: true);

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_invalid_envelope");
    }

    [Fact]
    public void Validate_PopulatedSectionWithoutContent_ReturnsSectionMismatch()
    {
        var envelope = CreateMortalEnvelope().Replace(
            "\"fateCards\": { \"state\": \"empty_by_design\", \"reason\": \"Его судьба пока не открыла отдельной карты.\" }",
            "\"fateCards\": { \"state\": \"populated\" }");
        using var document = JsonDocument.Parse(envelope);

        var issues = ActorMaterializationContract.Validate(
            document.RootElement,
            "npc",
            ActorMaterializationFamily.Mortal,
            CreateMortalEvidence(),
            requireEnvelope: true);

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_section_content_mismatch" &&
            issue.Section == "fateCards");
    }

    [Fact]
    public void Validate_EmptyByDesignSectionWithContent_ReturnsSectionMismatch()
    {
        var evidence = CreateMortalEvidence() with
        {
            SectionHasContent = new Dictionary<string, bool>(CreateMortalEvidence().SectionHasContent, StringComparer.Ordinal)
            {
                ["fateCards"] = true
            }
        };
        using var document = JsonDocument.Parse(CreateMortalEnvelope());

        var issues = ActorMaterializationContract.Validate(
            document.RootElement,
            "npc",
            ActorMaterializationFamily.Mortal,
            evidence,
            requireEnvelope: true);

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_section_content_mismatch" &&
            issue.Section == "fateCards");
    }

    [Fact]
    public void Validate_DeclaredCapabilityContradictsEvidence_ReturnsCapabilityMismatch()
    {
        var envelope = CreateMortalEnvelope().Replace("\"canTeach\": false", "\"canTeach\": true");
        using var document = JsonDocument.Parse(envelope);

        var issues = ActorMaterializationContract.Validate(
            document.RootElement,
            "npc",
            ActorMaterializationFamily.Mortal,
            CreateMortalEvidence(),
            requireEnvelope: true);

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Section == "canTeach");
    }

    [Fact]
    public void Validate_AfterlifeEnvelopeUsesMortalCapability_ReturnsInvalidEnvelopeIssue()
    {
        using var document = JsonDocument.Parse("""
        {
          "materialization": {
            "schemaVersion": 1,
            "materializationId": "mat_guardian_lira_turn_1",
            "actorType": "guardian",
            "actorId": "guardian_lira",
            "materializedAtTurn": 1,
            "state": "complete",
            "capabilities": {
              "canFight": true,
              "canTeach": true,
              "canTrade": false,
              "ownsItems": false
            },
            "sections": {
              "standardArts": { "state": "populated" },
              "specialArts": { "state": "empty_by_design", "reason": "Она ещё не создала личного искусства." },
              "customStates": { "state": "empty_by_design", "reason": "На душе нет особых состояний." },
              "fateCards": { "state": "empty_by_design", "reason": "Её карта судьбы ещё не открыта." },
              "relationships": { "state": "populated" },
              "agency": { "state": "populated" },
              "progressionHistory": { "state": "populated" }
            }
          }
        }
        """);
        var evidence = new ActorMaterializationEvidence(
            "guardian",
            "guardian_lira",
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["standardArts"] = true,
                ["specialArts"] = false,
                ["customStates"] = false,
                ["fateCards"] = false,
                ["relationships"] = true,
                ["agency"] = true,
                ["progressionHistory"] = true
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["canFight"] = true,
                ["canTeach"] = true,
                ["canTrade"] = false
            });

        var issues = ActorMaterializationContract.Validate(
            document.RootElement,
            "profile",
            ActorMaterializationFamily.Afterlife,
            evidence,
            requireEnvelope: true);

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_invalid_envelope");
    }

    [Fact]
    public void ValidateMortalNpc_NewNpcWithoutEnvelope_ReturnsMissingIssue()
    {
        using var document = JsonDocument.Parse("""
        {
          "NPCId": null,
          "initialId": "npc_station_medic",
          "activeSkills": [],
          "passiveSkills": [],
          "inventory": [],
          "fateCards": [],
          "personalQuests": [],
          "relationshipLevel": 0,
          "attitude": "Нейтралитет",
          "relationshipLock": { "isLocked": false }
        }
        """);

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "NPCsInScene");

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_missing");
    }

    [Fact]
    public void ValidateMortalNpc_ArbitrarySettingContentUsesStructureInsteadOfKeywords()
    {
        using var document = JsonDocument.Parse("""
        {
          "NPCId": null,
          "initialId": "npc_orbital_xenobiologist",
          "activeSkills": [
            { "skillId": "skill_phase_lattice", "name": "Фазовая решётка" }
          ],
          "passiveSkills": [],
          "inventory": [
            { "itemId": "item_spectral_calibrator", "name": "Спектральный калибратор" }
          ],
          "fateCards": [],
          "personalQuests": [],
          "relationshipLevel": 0,
          "attitude": "Нейтралитет",
          "relationshipLock": { "isLocked": false },
          "materialization": {
            "schemaVersion": 1,
            "materializationId": "mat_npc_orbital_xenobiologist_turn_2",
            "actorType": "mortal_npc",
            "actorId": "npc_orbital_xenobiologist",
            "materializedAtTurn": 2,
            "state": "complete",
            "capabilities": {
              "canFight": true,
              "canTeach": false,
              "canTrade": false,
              "ownsItems": true
            },
            "sections": {
              "skills": { "state": "populated" },
              "inventory": { "state": "populated" },
              "fateCards": { "state": "empty_by_design", "reason": "Его линия судьбы ещё не проявилась." },
              "personalQuests": { "state": "empty_by_design", "reason": "Сейчас он не просит героя о личной помощи." },
              "relationships": { "state": "populated" }
            }
          }
        }
        """);

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "NPCsInScene");

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateMortalNpc_UpdateWithPermanentId_DoesNotInferResendWithoutPreTurnAuthority()
    {
        using var document = JsonDocument.Parse(CreateMortalEnvelope()
            .Replace("{\n  \"materialization\"", "{\n  \"NPCId\": \"npc_arden\",\n  \"materialization\""));

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "UpdateNPCs");

        Assert.DoesNotContain(issues, issue => issue.Code == "actor_materialization_existing_resend_forbidden");
    }

    [Fact]
    public void ValidateAfterlifeProfile_CompleteArbitraryActor_ReturnsNoIssues()
    {
        using var document = JsonDocument.Parse("""
        {
          "actorType": "custom_afterlife_actor",
          "actorId": "actor_echo_of_the_last_signal",
          "standardArts": { "pressure": 2, "guard": 1 },
          "specialArts": [],
          "customStates": [],
          "fateCards": [],
          "relationships": [
            { "relationshipId": "rel_signal_player" }
          ],
          "goals": {
            "goalId": "goal_restore_signal",
            "shortTermGoal": "Найти источник помех",
            "longTermGoal": "Восстановить последний сигнал",
            "plan": "Сопоставить отголоски",
            "gmThoughtsSummary": "Сигнал становится яснее.",
            "updatedAtTurn": 3
          },
          "personalQuests": [],
          "completedActivities": [],
          "ledger": [
            { "entryId": "entry_signal_1" }
          ],
          "progressionLedger": [],
          "materialization": {
            "schemaVersion": 1,
            "materializationId": "mat_actor_echo_of_the_last_signal_turn_3",
            "actorType": "custom_afterlife_actor",
            "actorId": "actor_echo_of_the_last_signal",
            "materializedAtTurn": 3,
            "state": "complete",
            "capabilities": {
              "canFight": true,
              "canTeach": false,
              "canTrade": false
            },
            "sections": {
              "standardArts": { "state": "populated" },
              "specialArts": { "state": "empty_by_design", "reason": "Сущность ещё не сформировала личного искусства." },
              "customStates": { "state": "empty_by_design", "reason": "Сейчас на ней нет особых состояний." },
              "fateCards": { "state": "empty_by_design", "reason": "Её карта судьбы ещё не открыта." },
              "relationships": { "state": "populated" },
              "agency": { "state": "populated" },
              "progressionHistory": { "state": "populated" }
            }
          }
        }
        """);

        var issues = ActorMaterializationContract.ValidateAfterlifeProfile(
            document.RootElement,
            "profile",
            requireEnvelope: true,
            canTradeEvidence: false);

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateAfterlifeProfile_ZeroTierArtsDoNotCountAsCombatCapability()
    {
        const string json = """
        {
          "actorType": "resident",
          "actorId": "resident_quiet_archivist",
          "standardArts": { "pressure": 0, "guard": 0 },
          "specialArts": [],
          "customStates": [],
          "fateCards": [],
          "relationships": [],
          "ledger": [],
          "progressionLedger": [],
          "materialization": {
            "schemaVersion": 1,
            "materializationId": "mat_resident_quiet_archivist_turn_4",
            "actorType": "resident",
            "actorId": "resident_quiet_archivist",
            "materializedAtTurn": 4,
            "state": "complete",
            "capabilities": {
              "canFight": true,
              "canTeach": false,
              "canTrade": false
            },
            "sections": {
              "standardArts": { "state": "empty_by_design", "reason": "Резидент не владеет стандартными искусствами." },
              "specialArts": { "state": "empty_by_design", "reason": "Резидент не создал особого искусства." },
              "customStates": { "state": "empty_by_design", "reason": "Особые состояния отсутствуют." },
              "fateCards": { "state": "empty_by_design", "reason": "Карта судьбы ещё не открыта." },
              "relationships": { "state": "empty_by_design", "reason": "Устойчивые связи ещё не сформировались." },
              "agency": { "state": "empty_by_design", "reason": "Сейчас резидент не преследует самостоятельной цели." },
              "progressionHistory": { "state": "empty_by_design", "reason": "История развития ещё не началась." }
            }
          }
        }
        """;
        using var document = JsonDocument.Parse(json);

        var issues = ActorMaterializationContract.ValidateAfterlifeProfile(
            document.RootElement,
            "profile",
            requireEnvelope: true,
            canTradeEvidence: false);

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Section == "canFight");
    }

    [Theory]
    [InlineData("envelope")]
    [InlineData("capabilities")]
    [InlineData("sections")]
    [InlineData("disposition")]
    public void Validate_DuplicateClosedContractProperty_ReturnsDuplicatePropertyIssue(string duplicateLocation)
    {
        var json = duplicateLocation switch
        {
            "envelope" => CreateMortalEnvelope().Replace(
                "\"schemaVersion\": 1,",
                "\"schemaVersion\": 1, \"schemaVersion\": 1,"),
            "capabilities" => CreateMortalEnvelope().Replace(
                "\"canFight\": true,",
                "\"canFight\": true, \"canFight\": true,"),
            "sections" => CreateMortalEnvelope().Replace(
                "\"skills\": { \"state\": \"populated\" },",
                "\"skills\": { \"state\": \"populated\" }, \"skills\": { \"state\": \"populated\" },"),
            "disposition" => CreateMortalEnvelope().Replace(
                "\"skills\": { \"state\": \"populated\" }",
                "\"skills\": { \"state\": \"populated\", \"state\": \"populated\" }"),
            _ => throw new ArgumentOutOfRangeException(nameof(duplicateLocation))
        };
        using var document = JsonDocument.Parse(json);

        var issues = ActorMaterializationContract.Validate(
            document.RootElement,
            "npc",
            ActorMaterializationFamily.Mortal,
            CreateMortalEvidence(),
            requireEnvelope: true);

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_duplicate_property");
    }

    [Fact]
    public void Validate_DuplicateTopLevelMaterialization_ReturnsDuplicatePropertyIssue()
    {
        var json = CreateMortalEnvelope().Replace(
            "\"materialization\": {",
            "\"materialization\": null, \"materialization\": {");
        using var document = JsonDocument.Parse(json);

        var issues = ActorMaterializationContract.Validate(
            document.RootElement,
            "npc",
            ActorMaterializationFamily.Mortal,
            CreateMortalEvidence(),
            requireEnvelope: true);

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_duplicate_property" &&
            issue.FilePath == "npc.materialization");
    }

    [Fact]
    public void ValidateMortalNpc_InitialIdWithoutNpcId_RequiresFirstMaterializationEnvelope()
    {
        using var document = JsonDocument.Parse("""
        {
          "initialId": "npc_missing_identity_node"
        }
        """);

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "NPCsInScene");

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_missing");
    }

    [Theory]
    [InlineData("actorType")]
    [InlineData("actorId")]
    public void ValidateCanonicalAfterlifeProfile_MissingOuterCanonicalIdentity_ReturnsBindingIssue(
        string propertyName)
    {
        var profile = CreateAfterlifeProfile();
        profile.Remove(propertyName);
        using var document = JsonDocument.Parse(profile.ToJsonString());

        var issues = ActorMaterializationContract.ValidateCanonicalAfterlifeProfile(
            document.RootElement,
            "profile",
            requireEnvelope: true);

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_actor_binding_mismatch");
    }

    [Fact]
    public void ValidateMortalNpc_InventoryContainingOnlyNull_DoesNotCountAsInventoryContent()
    {
        var actor = CreateMortalNpcWithEnvelope();
        actor["inventory"] = new JsonArray { null };
        using var document = JsonDocument.Parse(actor.ToJsonString());

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "NPCsInScene");

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_section_content_mismatch" &&
            issue.Section == "inventory");
        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Section == "ownsItems");
    }

    [Fact]
    public void ValidateMortalNpc_EquippedItemOutsideNpcInventory_ReturnsReferenceIssue()
    {
        var actor = CreateMortalNpcWithEnvelope();
        actor["equippedItems"] = new JsonObject
        {
            ["MainHand"] = "item_not_owned_by_actor"
        };
        using var document = JsonDocument.Parse(actor.ToJsonString());

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "NPCsInScene");

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_inventory_reference_mismatch" &&
            issue.Section == "inventory");
    }

    [Fact]
    public void ValidateMortalNpc_NonObjectEquippedItems_ReturnsReferenceIssue()
    {
        var actor = CreateMortalNpcWithEnvelope();
        actor["equippedItems"] = "item_arden_blade";
        using var document = JsonDocument.Parse(actor.ToJsonString());

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "NPCsInScene");

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_inventory_reference_mismatch" &&
            issue.Section == "inventory");
    }

    [Fact]
    public void ValidateMortalNpc_NullPlaceholderQuest_DoesNotCountAsPopulatedContent()
    {
        var actor = CreateMortalNpcWithEnvelope();
        actor["personalQuests"] = new JsonArray
        {
            new JsonObject { ["placeholder"] = null }
        };
        using var document = JsonDocument.Parse(actor.ToJsonString());

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "NPCsInScene");

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_section_content_mismatch" &&
            issue.Section == "personalQuests");
    }

    [Fact]
    public void ValidateMortalNpc_UntouchedLegacyEquippedItemOutsideInventory_DoesNotApplyMaterializationReferenceRule()
    {
        var actor = new JsonObject
        {
            ["NPCId"] = "legacy_equipped_actor",
            ["inventory"] = new JsonArray(),
            ["equippedItems"] = new JsonObject
            {
                ["MainHand"] = "legacy_item_outside_embedded_inventory"
            }
        };
        using var document = JsonDocument.Parse(actor.ToJsonString());

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "NPCsInScene");

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_inventory_reference_mismatch");
    }

    [Fact]
    public void ValidateMortalNpc_TeacherSkillsContainingOnlyNull_DoNotSatisfyTeachingCapability()
    {
        var actor = CreateMortalNpcWithEnvelope();
        actor["teacherProfile"] = new JsonObject
        {
            ["canTeach"] = true,
            ["skills"] = new JsonArray { null }
        };
        actor["materialization"]!["capabilities"]!["canTeach"] = true;
        using var document = JsonDocument.Parse(actor.ToJsonString());

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "NPCsInScene");

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Section == "canTeach");
    }

    [Fact]
    public void ValidateMortalNpc_UsableTeacherSkillObject_SatisfiesTeachingCapability()
    {
        var actor = CreateMortalNpcWithEnvelope();
        actor["teacherProfile"] = new JsonObject
        {
            ["canTeach"] = true,
            ["skills"] = new JsonArray
            {
                new JsonObject
                {
                    ["skillId"] = "skill_archive_cipher",
                    ["skillName"] = "Архивный шифр",
                    ["displayName"] = "Архивный шифр",
                    ["skillKind"] = "passive_skill_mastery",
                    ["masteryLevel"] = 2
                }
            }
        };
        actor["materialization"]!["capabilities"]!["canTeach"] = true;
        using var document = JsonDocument.Parse(actor.ToJsonString());

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "NPCsInScene");

        Assert.DoesNotContain(issues, issue => issue.Code?.StartsWith("actor_materialization_", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ValidateMortalNpc_InvalidExplicitMerchantProfile_IsNotRescuedByNpcProse()
    {
        var actor = CreateMortalNpcWithEnvelope();
        actor["name"] = "Торговец, лавочник и merchant vendor";
        actor["role"] = "Торговец";
        actor["occupation"] = "Продаёт товары";
        actor["tradeState"] = new JsonObject
        {
            ["canTrade"] = true,
            ["merchantProfile"] = "NotARealMerchantProfile"
        };
        actor["materialization"]!["capabilities"]!["canTrade"] = true;
        using var document = JsonDocument.Parse(actor.ToJsonString());

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "NPCsInScene");

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Section == "canTrade");
    }

    [Fact]
    public void ValidateAfterlifeProfile_TierZeroArtsOccupySectionsWithoutGrantingCombatCapability()
    {
        var profile = CreateAfterlifeProfile();
        profile["standardArts"] = new JsonObject { ["guard"] = 0 };
        profile["specialArts"] = new JsonArray
        {
            new JsonObject
            {
                ["artId"] = "quiet_archive_ward",
                ["displayName"] = "Тихий архивный оберег",
                ["tier"] = 0
            }
        };
        profile["materialization"]!["capabilities"]!["canFight"] = false;
        profile["materialization"]!["sections"]!["standardArts"] = new JsonObject { ["state"] = "populated" };
        profile["materialization"]!["sections"]!["specialArts"] = new JsonObject { ["state"] = "populated" };
        using var document = JsonDocument.Parse(profile.ToJsonString());

        var issues = ActorMaterializationContract.ValidateAfterlifeProfile(
            document.RootElement,
            "profile",
            requireEnvelope: true,
            canTradeEvidence: false);

        Assert.DoesNotContain(issues, issue => issue.Code?.StartsWith("actor_materialization_", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ValidateAfterlifeProfile_StructuredMentorShowcase_SatisfiesTeachingCapability()
    {
        var profile = CreateAfterlifeProfile();
        profile["standardArts"] = new JsonObject { ["guard"] = 0 };
        profile["mentorTrainingShowcase"] = new JsonObject
        {
            ["offers"] = new JsonArray
            {
                new JsonObject
                {
                    ["offerId"] = "mentor_guard_1",
                    ["targetKind"] = "standard_spiritual_art",
                    ["targetId"] = "guard",
                    ["targetName"] = "Защита",
                    ["sourceCap"] = 1
                }
            }
        };
        profile["materialization"]!["capabilities"]!["canFight"] = false;
        profile["materialization"]!["capabilities"]!["canTeach"] = true;
        using var document = JsonDocument.Parse(profile.ToJsonString());

        var issues = ActorMaterializationContract.ValidateAfterlifeProfile(
            document.RootElement,
            "profile",
            requireEnvelope: true,
            canTradeEvidence: false);

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Section == "canTeach");
    }

    [Theory]
    [InlineData("masks")]
    [InlineData("progressionStrategy")]
    public void ValidateAfterlifeProfile_StructuredAgencySurface_OccupiesAgencySection(string agencySurface)
    {
        var profile = CreateAfterlifeProfile();
        profile.Remove("goals");
        if (agencySurface == "masks")
        {
            profile["masks"] = new JsonArray
            {
                new JsonObject { ["maskId"] = "mask_archive_keeper" }
            };
        }
        else
        {
            profile["progressionStrategy"] = new JsonObject
            {
                ["strategyId"] = "strategy_archive_keeper",
                ["summary"] = "Сохранять архив.",
                ["priorityOrder"] = new JsonArray("guard")
            };
        }
        using var document = JsonDocument.Parse(profile.ToJsonString());

        var issues = ActorMaterializationContract.ValidateAfterlifeProfile(
            document.RootElement,
            "profile",
            requireEnvelope: true,
            canTradeEvidence: false);

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "actor_materialization_section_content_mismatch" &&
            issue.Section == "agency");
    }

    [Fact]
    public void ValidateCanonicalAfterlifeProfile_UnavailableTradeEvidenceFailsClosed()
    {
        var profile = CreateAfterlifeProfile();
        profile["materialization"]!["capabilities"]!["canTrade"] = true;
        using var document = JsonDocument.Parse(profile.ToJsonString());

        var issues = ActorMaterializationContract.ValidateCanonicalAfterlifeProfile(
            document.RootElement,
            "profile",
            requireEnvelope: true);

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Section == "canTrade");
    }

    [Fact]
    public void ValidateAfterlifeProfile_WhitespaceOnlyGoal_DoesNotCountAsAgencyContent()
    {
        var profile = CreateAfterlifeProfile();
        profile["goals"] = new JsonObject { ["goalId"] = " " };
        profile["materialization"]!["sections"]!["agency"] = new JsonObject { ["state"] = "populated" };
        using var document = JsonDocument.Parse(profile.ToJsonString());

        var issues = ActorMaterializationContract.ValidateAfterlifeProfile(
            document.RootElement,
            "profile",
            requireEnvelope: true,
            canTradeEvidence: false);

        Assert.Contains(issues, issue =>
            issue.Code == "actor_materialization_section_content_mismatch" &&
            issue.Section == "agency");
    }

    [Fact]
    public void ActorMaterializationSchema_ClosesFamilySpecificKeysAndWhitespaceStrings()
    {
        var repositoryRoot = FindRepositoryRoot();
        var schemaPath = Path.Combine(
            repositoryRoot,
            "specs",
            "1500-complete-actor-materialization",
            "contracts",
            "actor-materialization.schema.json");
        using var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var root = document.RootElement;

        Assert.Equal("^.*\\S.*$", root.GetProperty("properties").GetProperty("actorId").GetProperty("pattern").GetString());
        Assert.Equal("^.*\\S.*$", root.GetProperty("properties").GetProperty("materializationId").GetProperty("pattern").GetString());
        Assert.True(root.TryGetProperty("allOf", out var branches));
        var familyBranch = Assert.Single(branches.EnumerateArray());
        var mortal = familyBranch.GetProperty("then").GetProperty("properties");
        var afterlife = familyBranch.GetProperty("else").GetProperty("properties");
        var sectionDisposition = root.GetProperty("$defs").GetProperty("sectionDisposition");
        var emptyByDesignDisposition = sectionDisposition
            .GetProperty("oneOf")
            .EnumerateArray()
            .Single(option => option.GetProperty("properties").GetProperty("state").GetProperty("const").GetString() == "empty_by_design");

        AssertClosedObjectSchema(
            mortal.GetProperty("capabilities"),
            "canFight", "canTeach", "canTrade", "ownsItems");
        AssertClosedObjectSchema(
            afterlife.GetProperty("capabilities"),
            "canFight", "canTeach", "canTrade");
        AssertClosedObjectSchema(
            mortal.GetProperty("sections"),
            "skills", "inventory", "fateCards", "personalQuests", "relationships");
        AssertClosedObjectSchema(
            afterlife.GetProperty("sections"),
            "standardArts", "specialArts", "customStates", "fateCards", "relationships", "agency", "progressionHistory");
        Assert.False(mortal.GetProperty("sections").GetProperty("additionalProperties").GetBoolean());
        Assert.False(afterlife.GetProperty("sections").GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            "^.*\\S.*$",
            emptyByDesignDisposition.GetProperty("properties").GetProperty("reason").GetProperty("pattern").GetString());
    }

    [Fact]
    public void ValidateUniqueMaterializationIds_DuplicateIdsReturnIssue()
    {
        using var first = JsonDocument.Parse(CreateMortalEnvelope());
        using var second = JsonDocument.Parse(CreateMortalEnvelope());

        var issues = ActorMaterializationContract.ValidateUniqueMaterializationIds(
            new[]
            {
                (first.RootElement, "first", "mortal_npc", "npc_arden"),
                (second.RootElement, "second", "mortal_npc", "npc_other")
            });

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_duplicate_id");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ValidateAfterlifeProfile_AuthoritativeTradeEvidence_IsComparedWhenKnown(
        bool authoritativeCanTrade,
        bool expectMismatch)
    {
        var profile = CreateAfterlifeProfile();
        profile["materialization"]!["capabilities"]!["canTrade"] = true;
        using var document = JsonDocument.Parse(profile.ToJsonString());

        var issues = ActorMaterializationContract.ValidateAfterlifeProfile(
            document.RootElement,
            "profile",
            requireEnvelope: true,
            canTradeEvidence: authoritativeCanTrade);

        Assert.Equal(
            expectMismatch,
            issues.Any(issue =>
                issue.Code == "actor_materialization_capability_mismatch" &&
                issue.Section == "canTrade"));
    }

    [Theory]
    [InlineData("Guardian", "guardian_case_variant")]
    [InlineData("player_soul", "player_soul")]
    public void ValidateAfterlifeProfile_NonCanonicalOrPlayerActorType_ReturnsActorTypeIssue(
        string actorType,
        string actorId)
    {
        var profile = CreateAfterlifeProfile(actorType, actorId);
        using var document = JsonDocument.Parse(profile.ToJsonString());

        var issues = ActorMaterializationContract.ValidateAfterlifeProfile(
            document.RootElement,
            "profile",
            requireEnvelope: true,
            canTradeEvidence: false);

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_invalid_actor_type");
    }

    private static ActorMaterializationEvidence CreateMortalEvidence() => new(
        ActorType: "mortal_npc",
        ActorId: "npc_arden",
        SectionHasContent: new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["skills"] = true,
            ["inventory"] = true,
            ["fateCards"] = false,
            ["personalQuests"] = true,
            ["relationships"] = true
        },
        CapabilityEvidence: new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["canFight"] = true,
            ["canTeach"] = false,
            ["canTrade"] = false,
            ["ownsItems"] = true
        });

    private static JsonObject CreateMortalNpcWithEnvelope()
    {
        var actor = JsonNode.Parse(CreateMortalEnvelope())!.AsObject();
        actor["NPCId"] = null;
        actor["initialId"] = "npc_arden";
        actor["activeSkills"] = new JsonArray
        {
            new JsonObject
            {
                ["skillId"] = "skill_arden_guard",
                ["name"] = "Стойка хранителя"
            }
        };
        actor["passiveSkills"] = new JsonArray();
        actor["inventory"] = new JsonArray
        {
            new JsonObject
            {
                ["itemId"] = "item_arden_blade",
                ["existedId"] = "item_arden_blade",
                ["name"] = "Клинок Ардена"
            }
        };
        actor["equippedItems"] = new JsonObject();
        actor["fateCards"] = new JsonArray();
        actor["personalQuests"] = new JsonArray
        {
            new JsonObject { ["questId"] = "quest_arden_archive" }
        };
        actor["relationshipLevel"] = 10;
        actor["attitude"] = "Нейтралитет";
        actor["relationshipLock"] = new JsonObject { ["isLocked"] = false };
        return actor;
    }

    private static JsonObject CreateAfterlifeProfile(
        string actorType = "guardian",
        string actorId = "guardian_archive_keeper")
    {
        var profile = JsonNode.Parse(
            """
            {
              "actorType": "guardian",
              "actorId": "guardian_archive_keeper",
              "standardArts": { "guard": 1 },
              "specialArts": [],
              "customStates": [],
              "fateCards": [],
              "relationships": [],
              "goals": {
                "goalId": "goal_preserve_archive"
              },
              "ledger": [
                { "entryId": "entry_archive_keeper" }
              ],
              "progressionLedger": [],
              "materialization": {
                "schemaVersion": 1,
                "materializationId": "mat_guardian_archive_keeper_turn_1",
                "actorType": "guardian",
                "actorId": "guardian_archive_keeper",
                "materializedAtTurn": 1,
                "state": "complete",
                "capabilities": {
                  "canFight": true,
                  "canTeach": false,
                  "canTrade": false
                },
                "sections": {
                  "standardArts": { "state": "populated" },
                  "specialArts": { "state": "empty_by_design", "reason": "Личное искусство ещё не создано." },
                  "customStates": { "state": "empty_by_design", "reason": "Особых состояний сейчас нет." },
                  "fateCards": { "state": "empty_by_design", "reason": "Карта судьбы ещё не открыта." },
                  "relationships": { "state": "empty_by_design", "reason": "Устойчивые связи ещё не сложились." },
                  "agency": { "state": "populated" },
                  "progressionHistory": { "state": "populated" }
                }
              }
            }
            """)!.AsObject();
        profile["actorType"] = actorType;
        profile["actorId"] = actorId;
        profile["materialization"]!["actorType"] = actorType;
        profile["materialization"]!["actorId"] = actorId;
        return profile;
    }

    private static string CreateMortalEnvelope() => """
    {
      "materialization": {
        "schemaVersion": 1,
        "materializationId": "mat_npc_arden_turn_1",
        "actorType": "mortal_npc",
        "actorId": "npc_arden",
        "materializedAtTurn": 1,
        "state": "complete",
        "capabilities": {
          "canFight": true,
          "canTeach": false,
          "canTrade": false,
          "ownsItems": true
        },
        "sections": {
          "skills": { "state": "populated" },
          "inventory": { "state": "populated" },
          "fateCards": { "state": "empty_by_design", "reason": "Его судьба пока не открыла отдельной карты." },
          "personalQuests": { "state": "populated" },
          "relationships": { "state": "populated" }
        }
      }
    }
    """;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "specs")) &&
                File.Exists(Path.Combine(
                    directory.FullName,
                    "BookOfEternityClient.Tests",
                    "BookOfEternityClient.Tests.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static void AssertClosedObjectSchema(JsonElement schema, params string[] expectedPropertyNames)
    {
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            expectedPropertyNames.OrderBy(name => name, StringComparer.Ordinal),
            schema.GetProperty("required")
                .EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty)
                .OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(
            expectedPropertyNames.OrderBy(name => name, StringComparer.Ordinal),
            schema.GetProperty("properties")
                .EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
    }
}
