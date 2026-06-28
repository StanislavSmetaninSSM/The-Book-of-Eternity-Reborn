using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GuardianSystemRegressionTests : IDisposable
{
    private const string EmptyGuardianProjectTrackerJson =
        "{\n  \"activeProjects\": [],\n  \"completedProjects\": [],\n  \"temporaryProjectModifiers\": []\n}";

    private const string EmptyGuardianPowerJournalJson =
        "{\n  \"entries\": []\n}";

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public GuardianSystemRegressionTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-guardian-system-regressions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "sessionId": "test-session", "requestId": "test-request", "turnNumber": 12 }
        """).GetAwaiter().GetResult();
        _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1
        }
        """).GetAwaiter().GetResult();
        EnsureBootstrapFile("game_state/meta/achievements.json", """
        {
          "unlockedAchievements": [],
          "trackedProgress": [],
          "stats": {
            "totalUnlocked": 0,
            "byCategory": {
              "combat": 0,
              "exploration": 0,
              "story": 0,
              "social": 0,
              "crafting": 0,
              "meta": 0,
              "death": 0,
              "secret": 0
            },
            "byRarity": {
              "common": 0,
              "uncommon": 0,
              "rare": 0,
              "epic": 0,
              "legendary": 0
            }
          }
        }
        """);
        EnsureBootstrapFile("lore/codex_entries.json", """
        {
          "entries": [],
          "totalEntries": 0,
          "categories": {
            "cosmology": 0,
            "geography": 0,
            "history": 0,
            "cultures": 0,
            "creatures": 0,
            "characters": 0,
            "artifacts": 0,
            "factions": 0,
            "magic": 0,
            "other": 0
          }
        }
        """);
        EnsureBootstrapFile("lore/current_world/geography.json", """
        {
          "regions": [
            {
              "regionId": "eternia_capital",
              "name": "Этерния",
              "type": "capital_city",
              "description": "Столица Валендрии: каменные набережные, туманные кварталы, дворянские особняки и гильдейские склады.",
              "knownLocations": [
                "Поместье Вальмонт",
                "Купеческий квартал",
                "Никельная набережная",
                "Северные ворота"
              ]
            }
          ]
        }
        """);
        EnsureBootstrapFile("lore/current_world/history.json", """
        {
          "eras": [],
          "recentEvents": [
            {
              "eventId": "valmont_letter_night",
              "title": "Письмо в покоях Вальмонта",
              "summary": "В покоях Асурана обнаружено письмо с незнакомой печатью: переплетенные крылья и полумесяц."
            }
          ]
        }
        """);
        EnsureBootstrapFile("lore/current_world/cultures.json", """
        {
          "cultures": [
            {
              "cultureId": "valendrian_nobility",
              "name": "Валендрийская знать",
              "values": [
                "родовая честь",
                "долги крови",
                "сдержанная демонстрация власти"
              ],
              "description": "Знать Валендрии говорит намеками, хранит семейные архивы и предпочитает решать опасные дела без публичного скандала."
            }
          ]
        }
        """);
        EnsureBootstrapFile("lore/current_world/threats.json", """
        {
          "threats": [
            {
              "threatId": "court_intrigue_valmont",
              "name": "Интрига вокруг дома Вальмонт",
              "severity": "local",
              "description": "Письмо, руническая перчатка и ночной гость связывают личную безопасность Асурана с более широким заговором."
            }
          ]
        }
        """);
        EnsureBootstrapFile("lore/chaos_sea/soul_system_lore.json", """
        {
          "soulLifecycle": {
            "preIncarnation": "Душа ожидает нового воплощения в Море Хаоса.",
            "memoryRetention": "Часть памяти переносится как Чернильные Перья."
          }
        }
        """);
        EnsureBootstrapFile("lore/chaos_sea/cosmology.json", """
        {
          "universalLaws": {
            "soulReincarnation": "Души проходят через Море Хаоса между жизнями."
          }
        }
        """);
        EnsureBootstrapFile("lore/chaos_sea/guardians_lore.json", """
        {
          "guardianRole": "Хранители сопровождают души между жизнями.",
          "abodes": []
        }
        """);
        EnsureBootstrapFile("lore/chaos_sea/player_chronicle.json", """
        {
          "lives": [],
          "notableReturns": []
        }
        """);
    }


    // Matrix case sources

    public static IEnumerable<object[]> InvalidResidentCurrentStateCases()
    {
        yield return MatrixCase(new CurrentStateCase("missing", DeleteFile: true));
        yield return MatrixCase(new CurrentStateCase("malformed", "{"));
        yield return MatrixCase(new CurrentStateCase("non_object_root", "[]"));
        yield return MatrixCase(new CurrentStateCase("contract_invalid_top_level", """
        {
          "foo": []
        }
        """));
        yield return MatrixCase(new CurrentStateCase("mixed_valid_and_unsupported_top_level", """
        {
          "entries": [],
          "foo": []
        }
        """));
        yield return MatrixCase(new CurrentStateCase("invalid_collection_shape", """
        {
          "entries": {}
        }
        """));
    }

    public static IEnumerable<object[]> InvalidSoulQuestCurrentStateCases()
    {
        yield return MatrixCase(new CurrentStateCase("missing", DeleteFile: true));
        yield return MatrixCase(new CurrentStateCase("malformed", "{"));
        yield return MatrixCase(new CurrentStateCase("non_object_root", "[]"));
        yield return MatrixCase(new CurrentStateCase("non_participating_empty_object", "{}"));
        yield return MatrixCase(new CurrentStateCase("contract_invalid_top_level", """
        {
          "foo": []
        }
        """));
        yield return MatrixCase(new CurrentStateCase("mixed_valid_and_unsupported_top_level", """
        {
          "quests": [],
          "foo": []
        }
        """));
        yield return MatrixCase(new CurrentStateCase("invalid_collection_shape", """
        {
          "quests": {}
        }
        """));
    }

    public static IEnumerable<object[]> InvalidCurrentWorldEventOwnerStateCases()
    {
        yield return MatrixCase(new CurrentStateCase("missing", DeleteFile: true));
        yield return MatrixCase(new CurrentStateCase("malformed", "{"));
        yield return MatrixCase(new CurrentStateCase("non_object_root", "\"broken\""));
        yield return MatrixCase(new CurrentStateCase("contract_invalid_top_level", """
        {
          "foo": []
        }
        """));
        yield return MatrixCase(new CurrentStateCase("mixed_valid_and_unsupported_top_level", """
        {
          "worldEventsLog": [],
          "foo": []
        }
        """));
        yield return MatrixCase(new CurrentStateCase("invalid_collection_shape", """
        {
            "worldEventsLog": {}
        }
        """));
    }

    public static IEnumerable<object[]> RivalCurrentStateFallbackMatrixCases()
    {
        yield return new object[] { new CurrentStateCase("missing", DeleteFile: true), false, true };
        yield return new object[] { new CurrentStateCase("non_participating_empty_object", "{}"), false, true };
        yield return new object[] { new CurrentStateCase("valid_readable", """
        {
          "arcs": [
            {
              "arcId": "arc_hunter"
            }
          ]
        }
        """), false, true };
        yield return new object[] { new CurrentStateCase("malformed", "{"), true, false };
        yield return new object[] { new CurrentStateCase("non_object_root", "[]"), true, false };
        yield return new object[] { new CurrentStateCase("contract_invalid_top_level", """
        {
          "foo": []
        }
        """), true, false };
        yield return new object[] { new CurrentStateCase("mixed_valid_and_unsupported_top_level", """
        {
          "arcs": [],
          "foo": []
        }
        """), true, false };
        yield return new object[] { new CurrentStateCase("invalid_collection_shape", """
        {
          "arcs": {}
        }
        """), true, false };
    }

    public static IEnumerable<object[]> BrokenPresentCurrentWorldEventOwnerStateCases()
    {
        yield return MatrixCase(new CurrentStateCase("malformed", "{"));
        yield return MatrixCase(new CurrentStateCase("non_object_root", "\"broken\""));
        yield return MatrixCase(new CurrentStateCase("contract_invalid_top_level", """
        {
          "foo": []
        }
        """));
        yield return MatrixCase(new CurrentStateCase("mixed_valid_and_unsupported_top_level", """
        {
          "worldEventsLog": [],
          "foo": []
        }
        """));
        yield return MatrixCase(new CurrentStateCase("invalid_collection_shape", """
        {
          "worldEventsLog": {}
        }
        """));
    }

    public static IEnumerable<object[]> InvalidManifestedCompanionNpcCurrentStateCases()
    {
        yield return MatrixCase(new CurrentStateCase("malformed_balanced_carrier_with_dependency", """
        {
          "NPCsInScene": [
            {
              "npcId": "npc_companion_alpha",
              "sourceAfterlifeResidentId": "resident_alpha",
              "sourceCompanionRelicId": "relic_missing"
            }
          ],
          "broken":
        """));
        yield return MatrixCase(new CurrentStateCase("shape_invalid_collection_with_dependency", """
        {
          "NPCsInScene": {
            "npcId": "npc_companion_alpha",
            "name": "Эхо спутника",
            "sourceCompanionRelicId": "relic_missing",
            "sourceAfterlifeResidentId": "resident_alpha"
          }
        }
        """));
        yield return MatrixCase(new CurrentStateCase("contract_invalid_top_level_with_carrier_dependency", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_companion_alpha",
              "name": "Эхо спутника",
              "sourceCompanionRelicId": "relic_missing",
              "sourceAfterlifeResidentId": "resident_alpha"
            }
          ],
          "foo": [
            {
              "npcId": "npc_ordinary",
              "name": "Лишний alias payload"
            }
          ]
        }
        """));
    }

    public static IEnumerable<object[]> InvalidNonManifestedNpcCurrentStateCases()
    {
        yield return MatrixCase(new CurrentStateCase("malformed_without_dependency", """
        {
          "NPCsInScene": [
            {
              "npcId": "npc_ordinary",
              "foo":
        """));
        yield return MatrixCase(new CurrentStateCase("malformed_unbounded_carrier_with_dependency_fields", """
        {
          "NPCsInScene": [
            {
              "npcId": "npc_companion_alpha",
              "sourceAfterlifeResidentId": "resident_alpha",
              "sourceCompanionRelicId":
        """));
        yield return MatrixCase(new CurrentStateCase("malformed_unbounded_carrier_followed_by_rename_dependency_fields", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_ordinary",
              "name": "Обычный прохожий"
            ,
          "NPCsRenameData": [
            {
              "oldName": "Обычный прохожий",
              "newName": "Переименованный прохожий",
              "sourceAfterlifeResidentId": "resident_alpha",
              "sourceCompanionRelicId": "relic_missing"
            }
          ]
        }
        """));
        yield return MatrixCase(new CurrentStateCase("malformed_unbounded_carrier_followed_by_alias_dependency_fields", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_ordinary",
              "name": "Обычный прохожий"
            ,
          "NPCs": [
            {
              "npcId": "npc_companion_alpha",
              "sourceAfterlifeResidentId": "resident_alpha",
              "sourceCompanionRelicId": "relic_missing"
            }
          ]
        }
        """));
        yield return MatrixCase(new CurrentStateCase("shape_invalid_collection_without_dependency", """
        {
            "NPCsInScene": {
              "npcId": "npc_ordinary",
              "name": "Обычный прохожий"
            }
        }
        """));
        yield return MatrixCase(new CurrentStateCase("contract_invalid_top_level_without_dependency", """
        {
          "foo": [
            {
              "npcId": "npc_ordinary",
              "name": "Обычный прохожий"
            }
          ]
        }
        """));
        yield return MatrixCase(new CurrentStateCase("contract_invalid_top_level_with_dependency_only_outside_carrier", """
        {
          "foo": [
            {
              "npcId": "npc_companion_alpha",
              "name": "Эхо спутника",
              "sourceCompanionRelicId": "relic_missing",
              "sourceAfterlifeResidentId": "resident_alpha"
            }
          ]
        }
        """));
        yield return MatrixCase(new CurrentStateCase("lifecycle_invalid_alias_without_dependency", """
        {
          "npcDataChanges": [
            {
              "npcId": "npc_ordinary",
              "name": "Обычный прохожий"
            }
          ]
        }
        """));
        yield return MatrixCase(new CurrentStateCase("lifecycle_invalid_alias_with_dependency_fields", """
        {
          "NPCs": [
            {
              "npcId": "npc_companion_alpha",
              "name": "Эхо спутника",
              "sourceCompanionRelicId": "relic_missing",
              "sourceAfterlifeResidentId": "resident_alpha"
            }
          ]
        }
        """));
        yield return MatrixCase(new CurrentStateCase("malformed_rename_payload_with_dependency_fields", """
        {
          "NPCsRenameData": [
            {
              "oldName": "Обычный прохожий",
              "newName": "Переименованный прохожий",
              "sourceAfterlifeResidentId": "resident_alpha",
              "sourceCompanionRelicId":
        """));
        yield return MatrixCase(new CurrentStateCase("malformed_carrier_string_literal_source_field_name_only", """
        {
          "NPCsInScene": [
            {
              "npcId": "npc_ordinary",
              "name": "Обычный прохожий",
              "notes": "В тексте упомянут sourceAfterlifeResidentId, но это не object key."
            }
          ],
          "broken":
        """));
    }

    public static IEnumerable<object[]> RivalBonusClueCurrentSoulStateMatrixCases()
    {
        yield return new object[] { new CurrentStateCase("missing", DeleteFile: true), true };
        yield return new object[] { new CurrentStateCase("malformed", "{"), true };
        yield return new object[] { new CurrentStateCase("non_object_root", "[]"), true };
        yield return new object[] { new CurrentStateCase("contract_invalid_top_level", """
        {
          "foo": []
        }
        """), true };
        yield return new object[] { new CurrentStateCase("mixed_valid_and_unsupported_top_level", """
        {
          "currentIncarnation": 3,
          "soulRelics": {
            "equipped": [],
            "stored": []
          },
          "foo": []
        }
        """), true };
        yield return new object[] { new CurrentStateCase("invalid_required_field_shape", """
        {
          "currentIncarnation": {}
        }
        """), true };
        yield return new object[] { new CurrentStateCase("readable_partial_supported", """
        {
          "currentIncarnation": 3
        }
        """), false };
        yield return new object[] { new CurrentStateCase("lifecycle_compatible_cross_incarnation_data", """
        {
          "soulName": "Тестовая Душа",
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "crossIncarnationData": {
            "legacyThreadId": "thread_alpha"
          }
        }
        """), false };
        yield return new object[] { new CurrentStateCase("valid_readable", """
        {
          "soulName": "Тестовая Душа",
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """), false };
    }

    public static IEnumerable<object[]> InvalidRivalBonusClueCurrentSoulStateCases()
    {
        yield return MatrixCase(new CurrentStateCase("missing", DeleteFile: true));
        yield return MatrixCase(new CurrentStateCase("malformed", "{"));
        yield return MatrixCase(new CurrentStateCase("non_object_root", "[]"));
        yield return MatrixCase(new CurrentStateCase("contract_invalid_top_level", """
        {
          "foo": []
        }
        """));
        yield return MatrixCase(new CurrentStateCase("mixed_valid_and_unsupported_top_level", """
        {
          "currentIncarnation": 3,
          "soulRelics": {
            "equipped": [],
            "stored": []
          },
          "foo": []
        }
        """));
        yield return MatrixCase(new CurrentStateCase("invalid_required_field_shape", """
        {
          "currentIncarnation": {}
        }
        """));
    }

    public static IEnumerable<object[]> ResidentRelicCurrentSoulStateMatrixCases()
    {
        yield return new object[] { new CurrentStateCase("missing", DeleteFile: true), true };
        yield return new object[] { new CurrentStateCase("malformed", "{"), true };
        yield return new object[] { new CurrentStateCase("non_object_root", "[]"), true };
        yield return new object[] { new CurrentStateCase("contract_invalid_top_level", """
        {
          "foo": []
        }
        """), true };
        yield return new object[] { new CurrentStateCase("mixed_valid_and_unsupported_top_level", """
        {
          "currentIncarnation": 3,
          "soulRelics": {
            "equipped": [],
            "stored": []
          },
          "foo": []
        }
        """), true };
        yield return new object[] { new CurrentStateCase("invalid_collection_shape", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": {}
          }
        }
        """), true };
        yield return new object[] { new CurrentStateCase("readable_partial_supported", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """), false };
        yield return new object[] { new CurrentStateCase("valid_readable", """
        {
          "soulName": "Тестовая Душа",
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": [],
            "stored": []
          }
        }
        """), false };
    }

    public static IEnumerable<object[]> InvalidResidentRelicCurrentSoulStateCases()
    {
        yield return MatrixCase(new CurrentStateCase("missing", DeleteFile: true));
        yield return MatrixCase(new CurrentStateCase("malformed", "{"));
        yield return MatrixCase(new CurrentStateCase("non_object_root", "[]"));
        yield return MatrixCase(new CurrentStateCase("contract_invalid_top_level", """
        {
          "foo": []
        }
        """));
        yield return MatrixCase(new CurrentStateCase("mixed_valid_and_unsupported_top_level", """
        {
          "currentIncarnation": 3,
          "soulRelics": [],
          "foo": []
        }
        """));
        yield return MatrixCase(new CurrentStateCase("invalid_collection_shape", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World",
          "soulRelics": {
            "equipped": {}
          }
        }
        """));
    }

    private async Task SeedRivalBonusClueValidationScenarioAsync(int targetIncarnation, int visibleRivalClueBudget = 1)
    {
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "domain": "Knowledge",
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": 80, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 35, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "domain": "Knowledge",
            "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
            "relationshipData": { "currentReputation": 80, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 35, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);

        var trackerJson = $$"""
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_major",
                "projectType": "lore_research",
                "projectTier": "major",
                "projectMode": "supportive",
                "projectName": "Раскрытие архива",
                "finalState": "Completed",
                "projectOutcomeAudit": {
                  "bonusLoreUnlocks": 1,
                  "questHookCount": 1,
                  "specialQuestLineUnlocks": 0,
                  "visibleRivalClueBonus": {{visibleRivalClueBudget}},
                  "unlockedLoreFragments": []
                },
                "effectState": {
                  "targetIncarnation": {{targetIncarnation}},
                  "bonusLoreUnlocksApplied": 1,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 1,
                  "guaranteedArchiveQuestGranted": 0,
                  "guaranteedArchiveQuestSpawned": 0,
                  "guaranteedArchiveQuestConsumed": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": {{visibleRivalClueBudget}},
                  "visibleRivalClueBudgetSpent": 0,
                  "archiveWarningTierBonusGranted": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """;
        await WriteRawAsync(GuardianProjectState.TrackerPath, trackerJson);
        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            $"test_backups/preturn_tracker_rival_bonus_validation_{targetIncarnation}.json",
            trackerJson);

        await WriteRawAsync(RivalSoulArcService.StatePath, """
        {
          "arcs": [
            {
              "arcId": "arc_hunter",
              "scope": "major",
              "arcType": "hostile_hunt",
              "status": "rising",
              "objective": "Find the player",
              "sponsorGuardianRef": {
                "mode": "guardianId",
                "guardianId": "guardian_alpha",
                "displayName": "Азалия"
              },
              "rivalSoul": {
                "rivalSoulId": "rival_1",
                "displayNameOrMoniker": "Багровый Охотник",
                "roleSummary": "Охотник rival-Хранителя",
                "isKnownToPlayer": true
              },
              "playerIntersection": {
                "targetsPlayerDirectly": true,
                "stakes": "Опасность для героя",
                "canBecomeSoulQuest": true,
                "recommendedCounterQuestTone": "urgent"
              },
              "milestones": [
                { "stage": 1, "title": "Слух", "summary": "О нём говорят", "visibleToPlayer": true }
              ],
              "currentStage": 1,
              "publicSignals": [
                {
                  "signalId": "sig_new",
                  "stage": 1,
                  "source": "Слух",
                  "description": "Наёмник ищет след героя",
                  "visibleToPlayer": true,
                  "bonusClueSourceProjectId": "research_major",
                  "bonusClueRevealId": "reveal_sig_new",
                  "bonusClueCost": 1
                }
              ],
              "resolution": { "outcome": "ongoing", "notes": "" }
            }
          ]
        }
        """);

    }

    private Task WriteDormantRivalBonusClueValidationArcAsync()
    {
        return WriteRawAsync(RivalSoulArcService.StatePath, """
        {
          "arcs": [
            {
              "arcId": "arc_hunter",
              "scope": "major",
              "arcType": "hostile_hunt",
              "status": "rising",
              "objective": "Find the player",
              "sponsorGuardianRef": {
                "mode": "guardianId",
                "guardianId": "guardian_alpha",
                "displayName": "Азалия"
              },
              "rivalSoul": {
                "rivalSoulId": "rival_1",
                "displayNameOrMoniker": "Багровый Охотник",
                "roleSummary": "Охотник rival-Хранителя",
                "isKnownToPlayer": true
              },
              "playerIntersection": {
                "targetsPlayerDirectly": true,
                "stakes": "Опасность для героя",
                "canBecomeSoulQuest": true,
                "recommendedCounterQuestTone": "urgent"
              },
              "milestones": [
                { "stage": 1, "title": "Слух", "summary": "О нём говорят", "visibleToPlayer": true }
              ],
              "currentStage": 1,
              "publicSignals": [],
              "resolution": { "outcome": "ongoing", "notes": "" }
            }
          ]
        }
        """);
    }

    private Task WriteAfterlifeResidentGuardianFixtureAsync(string appearanceDescription)
    {
        return WriteRawAsync("game_state/meta/guardians.json", $$"""
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "{{appearanceDescription}}"
              },
              "manifestationHistory": [],
              "domain": "Knowledge",
              "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
              "relationshipData": { "currentReputation": 80, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 35, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_alpha",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "{{appearanceDescription}}"
            },
            "manifestationHistory": [],
            "domain": "Knowledge",
            "abode": { "abodeId": "abode_alpha", "title": "Обитель Азалии" },
            "relationshipData": { "currentReputation": 80, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 35, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);
    }

    private Task WriteSingleAfterlifeResidentAsync(string displayName, string? grantedRelicId = null, string? linkedSoulQuestId = null)
    {
        var grantedRelicLine = string.IsNullOrWhiteSpace(grantedRelicId)
            ? string.Empty
            : $",\n              \"grantedRelicId\": \"{grantedRelicId}\"";
        var linkedSoulQuestLine = string.IsNullOrWhiteSpace(linkedSoulQuestId)
            ? string.Empty
            : $",\n              \"linkedSoulQuestId\": \"{linkedSoulQuestId}\"";

        return WriteRawAsync(GuardianAbodeResidentState.StatePath, $$"""
        {
          "entries": [
            {
              "residentId": "resident_alpha",
              "guardianId": "guardian_alpha",
              "abodeId": "abode_alpha",
              "displayName": "{{displayName}}"{{grantedRelicLine}}{{linkedSoulQuestLine}}
            }
          ],
          "rosterReceipts": [],
          "interactionReceipts": [],
          "historyLog": []
        }
        """);
    }

    private Task WriteManifestedCompanionNpcCoreAsync(
        string sourceCompanionRelicId,
        string sourceAfterlifeResidentId = "resident_alpha")
    {
        return WriteRawAsync("game_state/npcs/npc_core.json", $$"""
        {
          "NPCsInScene": [
            {
              "npcId": "npc_companion_alpha",
              "name": "Эхо спутника",
              "sourceCompanionRelicId": "{{sourceCompanionRelicId}}",
              "sourceAfterlifeResidentId": "{{sourceAfterlifeResidentId}}"
            }
          ]
        }
        """);
    }

    private static object[] MatrixCase(CurrentStateCase currentState) => new object[] { currentState };

    private async Task ApplyCurrentStateCaseAsync(string path, CurrentStateCase currentState)
    {
        if (currentState.DeleteFile)
        {
            _fs.DeleteFile(path);
            return;
        }

        await WriteRawAsync(path, currentState.Json ?? throw new InvalidOperationException($"Matrix case '{currentState.Name}' requires JSON content."));
    }

    private async Task ApplyRawCurrentStateCaseAsync(string path, CurrentStateCase currentState)
    {
        if (currentState.DeleteFile)
        {
            _fs.DeleteFile(path);
            return;
        }

        await _fs.WriteFileAtomicAsync(path, currentState.Json ?? throw new InvalidOperationException($"Matrix case '{currentState.Name}' requires JSON content."));
    }

    private static void AssertContainsIssueCodes(IEnumerable<ValidationIssue> issues, params string[] codes)
    {
        var issueList = issues.ToList();
        foreach (var code in codes)
        {
            Assert.Contains(issueList, issue =>
                string.Equals(issue.Code, code, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AssertDoesNotContainIssueCodes(IEnumerable<ValidationIssue> issues, params string[] codes)
    {
        var issueList = issues.ToList();
        foreach (var code in codes)
        {
            Assert.DoesNotContain(issueList, issue =>
                string.Equals(issue.Code, code, StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task WriteRawAsync(string path, string json, bool syncPendingSnapshotAuthority = true)
    {
        if (string.Equals(path, "game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedJson = NormalizeGuardianStateJson(json);
            await _fs.WriteFileAtomicAsync(path, normalizedJson);
            await EnsureValidatedPreTurnGuardiansSnapshotAsync(normalizedJson);
            await EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync();
            return;
        }

        if (string.Equals(path, "game_state/meta/soul_state.json", StringComparison.OrdinalIgnoreCase))
        {
            await _fs.WriteFileAtomicAsync(path, NormalizeSoulStateJson(json));
            return;
        }

        await _fs.WriteFileAtomicAsync(path, json);
        if (syncPendingSnapshotAuthority &&
            string.Equals(path, "game_state/control/pending_turn_snapshot.json", StringComparison.OrdinalIgnoreCase))
        {
            await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
        }
    }

    private async Task WriteGuardianRawWithoutValidatedSnapshotAsync(string json) =>
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", NormalizeGuardianStateJson(json));

    private void ResetValidatedPreTurnSnapshot()
    {
        var manifestPath = _fs.ResolvePath("game_state/control/pending_turn_snapshot.json");
        if (File.Exists(manifestPath))
            File.Delete(manifestPath);

        var snapshotDirectory = _fs.ResolvePath("game_state/control/pending_turn_snapshot");
        if (Directory.Exists(snapshotDirectory))
            Directory.Delete(snapshotDirectory, recursive: true);
    }

    private async Task AddCurrentSoulStateToValidatedPreTurnSnapshotAsync(string backupPath)
    {
        var soulStateJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json")
            ?? throw new InvalidOperationException("Expected current soul_state.json to exist in test fixture.");
        await WritePreTurnTrackedFileAsync("game_state/meta/soul_state.json", backupPath, soulStateJson);
    }

    private async Task AddCurrentWorldLoreToValidatedPreTurnSnapshotAsync(string backupRoot)
    {
        var loreRoot = _fs.ResolvePath("lore/current_world");
        if (!Directory.Exists(loreRoot))
            return;

        var gameSessionRoot = _fs.ResolvePath("");
        foreach (var file in Directory.GetFiles(loreRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(gameSessionRoot, file).Replace('\\', '/');
            var content = await File.ReadAllTextAsync(file);
            await WritePreTurnTrackedFileAsync(relativePath, $"{backupRoot}/{relativePath}", content);
        }
    }

    private async Task WritePreTurnTrackedFileAsync(string trackedPath, string backupPath, string json)
    {
        if (File.Exists(_fs.ResolvePath("game_state/control/pending_turn_snapshot.json")))
        {
            await AddTrackedFileToCurrentPendingTurnSnapshotAsync(trackedPath, backupPath, json);
            return;
        }

        await _fs.WriteFileAtomicAsync(backupPath, json);
        var snapshotPath = $"game_state/control/pending_turn_snapshot/{trackedPath}";
        await _fs.WriteFileAtomicAsync(snapshotPath, json);

        var manifest = CreateTestSnapshotManifest();
        manifest["files"] = new JsonObject
        {
            [trackedPath] = snapshotPath
        };
        manifest["snapshotFileHashes"] = new JsonObject
        {
            [trackedPath] = ComputeSha256(json)
        };
        manifest["rollbackBackups"] = new JsonObject
        {
            [trackedPath] = backupPath
        };
        manifest["rollbackBaselineFiles"] = new JsonArray(trackedPath);
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task WritePreTurnGuardiansTrackedFileAsync(string backupPath, string json) =>
        await WritePreTurnTrackedFileAsync("game_state/meta/guardians.json", backupPath, NormalizeGuardianStateJson(json));

    private async Task AddTrackedFileToCurrentPendingTurnSnapshotAsync(string trackedPath, string backupPath, string json)
    {
        await _fs.WriteFileAtomicAsync(backupPath, json);
        var snapshotPath = $"game_state/control/pending_turn_snapshot/{trackedPath}";
        await _fs.WriteFileAtomicAsync(snapshotPath, json);

        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        var files = manifest["files"] as JsonObject ?? new JsonObject();
        var snapshotHashes = manifest["snapshotFileHashes"] as JsonObject ?? new JsonObject();
        var rollbackBackups = manifest["rollbackBackups"] as JsonObject ?? new JsonObject();
        var rollbackBaselineFiles = manifest["rollbackBaselineFiles"] as JsonArray ?? new JsonArray();

        files[trackedPath] = snapshotPath;
        snapshotHashes[trackedPath] = ComputeSha256(json);
        rollbackBackups[trackedPath] = backupPath;
        if (!rollbackBaselineFiles.Any(node => string.Equals(node?.GetValue<string>(), trackedPath, StringComparison.OrdinalIgnoreCase)))
            rollbackBaselineFiles.Add(trackedPath);

        manifest["files"] = files;
        manifest["snapshotFileHashes"] = snapshotHashes;
        manifest["rollbackBackups"] = rollbackBackups;
        manifest["rollbackBaselineFiles"] = rollbackBaselineFiles;
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task RemoveTrackedFileFromCurrentPendingTurnSnapshotAsync(string trackedPath)
    {
        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        if (manifest["files"] is JsonObject files &&
            files[trackedPath] is JsonValue snapshotPathValue &&
            snapshotPathValue.TryGetValue<string>(out var snapshotPath) &&
            !string.IsNullOrWhiteSpace(snapshotPath))
        {
            var resolvedSnapshotPath = _fs.ResolvePath(snapshotPath);
            if (File.Exists(resolvedSnapshotPath))
                File.Delete(resolvedSnapshotPath);
            files.Remove(trackedPath);
        }

        if (manifest["snapshotFileHashes"] is JsonObject snapshotHashes)
            snapshotHashes.Remove(trackedPath);

        if (manifest["rollbackBackups"] is JsonObject rollbackBackups &&
            rollbackBackups[trackedPath] is JsonValue backupPathValue &&
            backupPathValue.TryGetValue<string>(out var backupPath) &&
            !string.IsNullOrWhiteSpace(backupPath))
        {
            var resolvedBackupPath = _fs.ResolvePath(backupPath);
            if (File.Exists(resolvedBackupPath))
                File.Delete(resolvedBackupPath);
            rollbackBackups.Remove(trackedPath);
        }

        if (manifest["rollbackBaselineFiles"] is JsonArray rollbackBaselineFiles)
        {
            var existingEntry = rollbackBaselineFiles
                .FirstOrDefault(node => string.Equals(node?.GetValue<string>(), trackedPath, StringComparison.OrdinalIgnoreCase));
            if (existingEntry != null)
                rollbackBaselineFiles.Remove(existingEntry);
        }

        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task RemoveTrackedSnapshotEntryFromCurrentPendingTurnSnapshotAsync(string trackedPath)
    {
        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        if (manifest["files"] is JsonObject files &&
            files[trackedPath] is JsonValue snapshotPathValue &&
            snapshotPathValue.TryGetValue<string>(out var snapshotPath) &&
            !string.IsNullOrWhiteSpace(snapshotPath))
        {
            var resolvedSnapshotPath = _fs.ResolvePath(snapshotPath);
            if (File.Exists(resolvedSnapshotPath))
                File.Delete(resolvedSnapshotPath);
            files.Remove(trackedPath);
        }

        if (manifest["snapshotFileHashes"] is JsonObject snapshotHashes)
            snapshotHashes.Remove(trackedPath);

        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task RemoveRollbackBackupFromCurrentPendingTurnSnapshotAsync(string trackedPath, string backupPath)
    {
        var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
        if (manifest["rollbackBackups"] is JsonObject rollbackBackups)
            rollbackBackups.Remove(trackedPath);

        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);

        var resolvedBackupPath = _fs.ResolvePath(backupPath);
        if (File.Exists(resolvedBackupPath))
            File.Delete(resolvedBackupPath);
    }

    private async Task EnsureValidatedPreTurnGuardiansSnapshotAsync(string currentGuardiansJson)
    {
        var snapshotRoot = BuildValidatedPreTurnGuardiansSnapshotRoot(currentGuardiansJson);
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            "test_backups/preturn_guardians_auto_baseline.json",
            snapshotRoot.ToJsonString());
    }

    private async Task EnsureValidatedPreTurnGuardianProjectTrackerSnapshotAsync()
    {
        if (File.Exists(_fs.ResolvePath("game_state/control/pending_turn_snapshot.json")))
        {
            var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
            if (manifest["files"] is JsonObject files &&
                files.ContainsKey(GuardianProjectState.TrackerPath))
            {
                return;
            }
        }

        await WritePreTurnTrackedFileAsync(
            GuardianProjectState.TrackerPath,
            "test_backups/preturn_guardian_tracker_auto_baseline.json",
            EmptyGuardianProjectTrackerJson);

        if (File.Exists(_fs.ResolvePath("game_state/control/pending_turn_snapshot.json")))
        {
            var manifest = await ReadObjectAsync("game_state/control/pending_turn_snapshot.json");
            if (manifest["files"] is JsonObject files &&
                files.ContainsKey(GuardianPowerEventState.JournalPath))
            {
                return;
            }
        }

        await WritePreTurnTrackedFileAsync(
            GuardianPowerEventState.JournalPath,
            "test_backups/preturn_abode_power_journal_auto_baseline.json",
            EmptyGuardianPowerJournalJson);
    }

    private async Task EnsureEmptyCurrentGuardianProjectTrackerAndPowerJournalAsync()
    {
        await WriteRawAsync(GuardianProjectState.TrackerPath, EmptyGuardianProjectTrackerJson);
        await WriteRawAsync(GuardianPowerEventState.JournalPath, EmptyGuardianPowerJournalJson);
    }

    private static string NormalizeGuardianStateJson(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();

        if (root["guardians"] is JsonArray guardians)
        {
            foreach (var guardianNode in guardians)
            {
                if (guardianNode is JsonObject guardian)
                    NormalizeGuardianCanonicalObject(guardian);
            }
        }

        EnsureGuardianRelationshipNetwork(root);

        if (root["activeGuardian"] is JsonObject activeGuardian)
            NormalizeGuardianCanonicalObject(activeGuardian);

        EnsureGuardianNavigationState(root);

        return root.ToJsonString();
    }

    public sealed record CurrentStateCase(string Name, string? Json = null, bool DeleteFile = false)
    {
        public override string ToString() => Name;
    }

    private static void EnsureGuardianRelationshipNetwork(JsonObject root)
    {
        if (root["guardians"] is not JsonArray guardians)
            return;

        var guardianObjects = guardians
            .OfType<JsonObject>()
            .Select(guardian => (
                Guardian: guardian,
                GuardianId: guardian["guardianId"]?.GetValue<string>(),
                CanonicalName: guardian["canonicalName"]?.GetValue<string>() ?? guardian["guardianId"]?.GetValue<string>() ?? "guardian_test"))
            .Where(item => !string.IsNullOrWhiteSpace(item.GuardianId))
            .Select(item => (item.Guardian, GuardianId: item.GuardianId!, item.CanonicalName))
            .ToList();
        if (guardianObjects.Count <= 1)
            return;

        foreach (var item in guardianObjects)
        {
            EnsureGuardianRelationshipEntries(
                item.Guardian,
                item.GuardianId!,
                guardianObjects);
        }

        if (root["activeGuardian"] is JsonObject activeGuardian)
        {
            var activeGuardianId = activeGuardian["guardianId"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(activeGuardianId))
            {
                EnsureGuardianRelationshipEntries(
                    activeGuardian,
                    activeGuardianId,
                    guardianObjects);
            }
        }
    }

    private static void EnsureGuardianRelationshipEntries(
        JsonObject guardian,
        string guardianId,
        IReadOnlyList<(JsonObject Guardian, string GuardianId, string CanonicalName)> guardianObjects)
    {
        if (guardian["guardianRelationships"] is not JsonArray relationships)
        {
            relationships = new JsonArray();
            guardian["guardianRelationships"] = relationships;
        }

        var existingTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relationNode in relationships)
        {
            if (relationNode is not JsonObject relation)
                continue;

            var targetGuardianId = relation["targetGuardianId"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(targetGuardianId))
                continue;

            existingTargets.Add(targetGuardianId);
            relation["attitudeScore"] ??= 0;
            relation["attitudeTier"] ??= "neutral";
            relation["reason"] ??= "Auto-generated canonical test relationship";
            if (!relation.ContainsKey("lastChangedAt"))
                relation["lastChangedAt"] = null;
        }

        foreach (var other in guardianObjects)
        {
            if (string.Equals(guardianId, other.GuardianId, StringComparison.OrdinalIgnoreCase) ||
                existingTargets.Contains(other.GuardianId))
            {
                continue;
            }

            relationships.Add(new JsonObject
            {
                ["targetGuardianId"] = other.GuardianId,
                ["attitudeScore"] = 0,
                ["attitudeTier"] = "neutral",
                ["reason"] = "Auto-generated canonical test relationship",
                ["lastChangedAt"] = null
            });
        }
    }

    private static JsonObject BuildValidatedPreTurnGuardiansSnapshotRoot(string currentGuardiansJson)
    {
        var currentRoot = JsonNode.Parse(currentGuardiansJson)?.AsObject() ?? new JsonObject();
        var snapshotRoot = new JsonObject();

        if (currentRoot["guardians"] is JsonNode guardians)
            snapshotRoot["guardians"] = guardians.DeepClone();
        if (currentRoot["activeGuardian"] is JsonNode activeGuardian)
            snapshotRoot["activeGuardian"] = activeGuardian.DeepClone();
        if (currentRoot["chaosSeaNavigation"] is JsonNode navigation)
            snapshotRoot["chaosSeaNavigation"] = navigation.DeepClone();

        return snapshotRoot;
    }

    private static string NormalizeSoulStateJson(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
        root["soulName"] ??= "Тестовая Душа";
        root["currentRealm"] ??= "Chaos Sea";
        root["currentIncarnation"] ??= 1;
        return root.ToJsonString();
    }

    private static void NormalizeGuardianCanonicalObject(JsonObject guardian)
    {
        var guardianId = guardian["guardianId"]?.GetValue<string>() ?? "guardian_test";
        var canonicalName = guardian["canonicalName"]?.GetValue<string>() ??
                            guardian["name"]?.GetValue<string>() ??
                            guardianId;

        guardian["guardianId"] ??= guardianId;
        guardian["canonicalName"] ??= canonicalName;
        guardian["domain"] ??= "Порог Сна";
        guardian["manifestationHistory"] ??= new JsonArray();
        guardian["guardianRelationships"] ??= new JsonArray();

        if (guardian["nameVariants"] is not JsonObject nameVariants)
        {
            nameVariants = new JsonObject();
            guardian["nameVariants"] = nameVariants;
        }

        nameVariants["default"] ??= canonicalName;
        if (nameVariants["feminine"] is null)
            nameVariants.Remove("feminine");
        if (nameVariants["masculine"] is null)
            nameVariants.Remove("masculine");
        if (nameVariants["neutral"] is null)
            nameVariants.Remove("neutral");

        if (guardian["manifestation"] is not JsonObject manifestation)
        {
            manifestation = new JsonObject();
            guardian["manifestation"] = manifestation;
        }

        manifestation["currentDisplayName"] ??= canonicalName;
        manifestation["formFlexibility"] ??= "selective";
        manifestation["currentPresentationStyle"] ??= "feminine";
        manifestation["currentPronouns"] ??= "она/её";
        manifestation["appearanceDescription"] ??= "Тестовая форма.";

        if (guardian["personalityProfile"] is not JsonObject personalityProfile)
        {
            personalityProfile = new JsonObject();
            guardian["personalityProfile"] = personalityProfile;
        }

        personalityProfile["archetype"] ??= "Tide Keeper";
        personalityProfile["speechPattern"] ??= "Measured and tidal";
        if (personalityProfile["coreValues"] is not JsonArray coreValues || coreValues.Count == 0)
        {
            personalityProfile["coreValues"] = new JsonArray("balance", "memory", "patience");
        }

        if (guardian["relationshipData"] is not JsonObject relationshipData)
        {
            relationshipData = new JsonObject();
            guardian["relationshipData"] = relationshipData;
        }

        relationshipData["currentReputation"] ??= 0;
        relationshipData["reputationHistory"] ??= new JsonArray();
        if (!relationshipData.ContainsKey("lastInteraction"))
            relationshipData["lastInteraction"] = null;

        if (guardian["abodePower"] is not JsonObject abodePower)
        {
            abodePower = new JsonObject();
            guardian["abodePower"] = abodePower;
        }

        abodePower["currentPower"] ??= 10;
        abodePower["tier"] ??= "Хрупкая";
        abodePower["lastUpdatedAt"] ??= "2026-03-24T00:00:00Z";
        abodePower["history"] ??= new JsonArray();

        if (guardian["abode"] is not JsonObject abode)
        {
            abode = new JsonObject();
            guardian["abode"] = abode;
        }

        abode["abodeId"] ??= $"abode_{guardianId}";
        abode["title"] ??= $"Обитель {canonicalName}";
        abode["name"] ??= abode["title"]?.DeepClone();
        abode["isDiscovered"] ??= true;

        if (guardian["questManagement"] is not JsonObject questManagement)
        {
            questManagement = new JsonObject();
            guardian["questManagement"] = questManagement;
        }

        questManagement["availableQuests"] ??= new JsonArray();
        questManagement["activeQuests"] ??= new JsonArray();
        questManagement["completedQuests"] ??= new JsonArray();

        if (guardian["loreFragments"] is not JsonArray loreFragments || loreFragments.Count < 7)
        {
            guardian["loreFragments"] = new JsonArray(
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_1", ["category"] = "personal_history", ["title"] = "След памяти", ["content"] = null, ["requiredReputation"] = 0 },
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_2", ["category"] = "cosmic_secret", ["title"] = "Тайна прилива", ["content"] = null, ["requiredReputation"] = 50 },
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_3", ["category"] = "domain_mastery", ["title"] = "Узел домена", ["content"] = null, ["requiredReputation"] = 130 },
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_4", ["category"] = "lost_world", ["title"] = "Утраченный берег", ["content"] = null, ["requiredReputation"] = 230 },
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_5", ["category"] = "other_guardians", ["title"] = "Сеть хранителей", ["content"] = null, ["requiredReputation"] = 0 },
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_6", ["category"] = "soul_mechanics", ["title"] = "Механика души", ["content"] = null, ["requiredReputation"] = 50 },
                new JsonObject { ["fragmentId"] = $"{guardianId}_lore_7", ["category"] = "personal_history", ["title"] = "Возврат волны", ["content"] = null, ["requiredReputation"] = 130 });
        }

        if (guardian["gachaSystem"] is not JsonObject gachaSystem)
        {
            gachaSystem = new JsonObject();
            guardian["gachaSystem"] = gachaSystem;
        }

        gachaSystem["chargesUsedThisReturn"] ??= 0;
        gachaSystem["gachaHistory"] ??= new JsonArray();

        if (guardian["mood"] is not JsonObject mood)
        {
            mood = new JsonObject();
            guardian["mood"] = mood;
        }

        mood["current"] ??= "focused";
        mood["intensity"] ??= 40;
        mood["reason"] ??= "Тестовый guardian baseline.";
        mood["since"] ??= 12;

        GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignore temp cleanup failures
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
            File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), overwrite: true);

        foreach (var directory in Directory.EnumerateDirectories(sourceDir))
            CopyDirectory(directory, Path.Combine(destinationDir, Path.GetFileName(directory)));
    }

    private void EnsureBootstrapFile(string path, string json)
    {
        if (!File.Exists(_fs.ResolvePath(path)))
            _fs.WriteFileAtomicAsync(path, json).GetAwaiter().GetResult();
    }
}
