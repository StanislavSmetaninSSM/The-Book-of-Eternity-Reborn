using System.Text.Json;
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
    public void ValidateMortalNpc_ExistingUpdateResendsEnvelope_ReturnsResendIssue()
    {
        using var document = JsonDocument.Parse(CreateMortalEnvelope()
            .Replace("{\n  \"materialization\"", "{\n  \"NPCId\": \"npc_arden\",\n  \"materialization\""));

        var issues = ActorMaterializationContract.ValidateMortalNpc(
            document.RootElement,
            "npc",
            "UpdateNPCs");

        Assert.Contains(issues, issue => issue.Code == "actor_materialization_existing_resend_forbidden");
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
}
