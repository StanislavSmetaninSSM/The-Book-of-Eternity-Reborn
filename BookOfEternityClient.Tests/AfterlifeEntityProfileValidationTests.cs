using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeEntityProfileValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AfterlifeEntityProfileValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-entity-profile-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidAfterlifeEntityProfile_PassesProfileValidation()
    {
        await WriteProfileStateAsync(BuildValidProfileJson());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_entity_profile_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidAfterlifeRelationshipGate_PassesProfileValidation()
    {
        await WriteProfileStateAsync(BuildValidProfileJson().Replace(
            "\"soulDissipationTier\": 1,",
            """
              "relationships": [
                {
                  "relationshipId": "guardian_mirror_player_trust",
                  "axis": "trust",
                  "targetActorType": "player_soul",
                  "targetActorId": "player_soul",
                  "value": 49,
                  "relationshipTier": "trust_breakthrough_required",
                  "relationshipLock": {
                    "lockState": "positive_locked",
                    "direction": "positive",
                    "threshold": 50,
                    "breakthroughQuestId": "quest_mirror_oath_trial",
                    "reason": "Хранитель не доверится глубже без личного испытания.",
                    "evidence": "Игрок приблизился к порогу доверия.",
                    "updatedAtTurn": 41
                  },
                  "relationshipGateQuests": [
                    {
                      "questId": "quest_mirror_oath_trial",
                      "questType": "breakthrough",
                      "status": "active",
                      "title": "Суд зеркальной клятвы",
                      "sceneSummary": "Личное испытание доверия.",
                      "successCondition": "Душа выбирает правду.",
                      "gmThoughtsSummary": "Это не бытовой fetch quest.",
                      "updatedAtTurn": 41
                    }
                  ]
                }
              ],
              "soulDissipationTier": 1,
            """,
            StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_entity_profile_relationship_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentTeachableSpecialArtRequiresCombatEffect()
    {
        await WriteProfileStateAsync(BuildCurrentProfileUpdateWithSpecialArt(
            """
                  "effectSummary": "При успехе отражает часть давления в сторону противника."
            """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_missing_combat_effect", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentTeachableSpecialArtAcceptsMeaningfulCombatEffect()
    {
        await WriteProfileStateAsync(BuildCurrentProfileUpdateWithSpecialArt(
            """
                  "effectSummary": "При успехе отражает часть давления в сторону противника.",
                  "combatEffect": {
                    "summary": "Отражённая грань превращает успешную защиту в одно темповое окно для следующего действия.",
                    "trigger": "Когда guard этого искусства полностью блокирует прямое pressure.",
                    "mechanicalAxis": "tempoAdvantage",
                    "allowedPayoff": "Можно создать одно tempoAdvantage.player с источником Зеркальной Защиты.",
                    "limit": "Один раз за конфликт, пока окно не будет потрачено или погашено контрприёмом.",
                    "auditRequirement": "specialArtAudit.effectNote должен назвать блокированное pressure и созданное tempoAdvantage."
                  }
            """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_entity_profile_special_art_", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(issues, issue =>
            issue.FilePath.StartsWith(AfterlifeEntityProfileState.StatePath, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(
        """
              "effectSummary": "При успехе отражает часть давления в сторону противника.",
              "combatEffect": {
                "summary": "Особый эффект применяется.",
                "trigger": "Когда guard этого искусства полностью блокирует прямое pressure.",
                "mechanicalAxis": "tempoAdvantage",
                "allowedPayoff": "Можно создать одно tempoAdvantage.player с источником Зеркальной Защиты.",
                "limit": "Один раз за конфликт, пока окно не будет потрачено.",
                "auditRequirement": "specialArtAudit.effectNote должен назвать созданное tempoAdvantage."
              }
        """,
        "afterlife_entity_profile_special_art_invalid_combat_effect_summary")]
    [InlineData(
        """
              "effectSummary": "При успехе отражает часть давления в сторону противника.",
              "combatEffect": {
                "summary": "Отражённая грань превращает успешную защиту в одно темповое окно для следующего действия.",
                "trigger": "Когда guard этого искусства полностью блокирует прямое pressure.",
                "mechanicalAxis": "hitPoints",
                "allowedPayoff": "Можно нанести урон HP противника.",
                "limit": "Один раз за конфликт.",
                "auditRequirement": "specialArtAudit.effectNote должен назвать HP-урон."
              }
        """,
        "afterlife_entity_profile_special_art_invalid_combat_effect_axis")]
    [InlineData(
        """
              "effectSummary": "При успехе отражает часть давления в сторону противника.",
              "combatEffect": {
                "summary": "Отражённая грань превращает успешную защиту в одно темповое окно для следующего действия.",
                "mechanicalAxis": "tempoAdvantage",
                "allowedPayoff": "Можно создать одно tempoAdvantage.player с источником Зеркальной Защиты.",
                "limit": "Один раз за конфликт.",
                "auditRequirement": "specialArtAudit.effectNote должен назвать созданное tempoAdvantage."
              }
        """,
        "afterlife_entity_profile_special_art_combat_effect_missing_required_field")]
    [InlineData(
        """
              "effectSummary": "При успехе отражает часть давления в сторону противника.",
              "combatEffect": {
                "summary": "Пассивный безлимитный бонус обходит baseOperation и tactical matrix.",
                "trigger": "Всегда активен.",
                "mechanicalAxis": "rollMode",
                "allowedPayoff": "Всегда даёт great_advantage без условия.",
                "limit": "Безлимитно.",
                "auditRequirement": "specialArtAudit.effectNote может ничего не объяснять."
              }
        """,
        "afterlife_entity_profile_special_art_invalid_combat_effect_scope")]
    public async Task ValidateGameStateAsync_CurrentSpecialArtRejectsInvalidCombatEffect(
        string artPayload,
        string expectedCode)
    {
        await WriteProfileStateAsync(BuildCurrentProfileUpdateWithSpecialArt(artPayload));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LegacyPersistedSpecialArtWithoutCombatEffect_RemainsLoadable()
    {
        await WriteProfileStateAsync(BuildValidProfileJson());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_missing_combat_effect", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidAfterlifeActorMask_PassesProfileValidation()
    {
        await WriteProfileStateAsync(BuildValidProfileJson().Replace(
            "\"soulDissipationTier\": 1,",
            """
              "activeMaskId": "mask_wings_emissary",
              "masks": [
                {
                  "maskId": "mask_wings_emissary",
                  "displayName": "Посланник Белых Перьев",
                  "publicArchetype": "учтивый посредник",
                  "visiblePersonality": "обещает помощь и скрывает истинные мотивы",
                  "concealedTruth": "Маска скрывает агента Крыльев Ангелов.",
                  "directives": [
                    "Не произносить имя Сарефа первым.",
                    "Собирать сведения о памяти игрока."
                  ],
                  "revealConditions": [
                    "Игрок связывает белые перья с Крыльями Ангелов."
                  ],
                  "deceptionRisk": "high",
                  "linkedSarefAgentId": "saref_agent_white_feather",
                  "isRevealed": false,
                  "updatedAtTurn": 52
                }
              ],
              "soulDissipationTier": 1,
            """,
            StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_entity_profile_mask_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_NullActiveMaskId_RequiresTrueSelfKeyword()
    {
        await WriteProfileStateAsync(BuildValidProfileJson().Replace(
            "\"soulDissipationTier\": 1,",
            """
              "activeMaskId": null,
              "masks": [
                {
                  "maskId": "mask_wings_emissary",
                  "displayName": "Посланник Белых Перьев",
                  "publicArchetype": "учтивый посредник",
                  "visiblePersonality": "обещает помощь и скрывает истинные мотивы",
                  "concealedTruth": "Маска скрывает агента Крыльев Ангелов.",
                  "directives": ["Не произносить имя Сарефа первым."],
                  "revealConditions": ["Игрок связывает белые перья с Крыльями Ангелов."],
                  "deceptionRisk": "high",
                  "isRevealed": false
                }
              ],
              "soulDissipationTier": 1,
            """,
            StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_mask_active_requires_true_self", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidAfterlifeActorMaskCommands_PassesMaskValidation()
    {
        var json = BuildValidProfileJson().Replace(
            "\"soulDissipationTier\": 1,",
            """
              "activeMaskId": "mask_wings_emissary",
              "masks": [
                {
                  "maskId": "mask_wings_emissary",
                  "displayName": "Посланник Белых Перьев",
                  "publicArchetype": "учтивый посредник",
                  "visiblePersonality": "говорит мягко и уводит от имени Сарефа",
                  "concealedTruth": "Маска скрывает агента Крыльев Ангелов.",
                  "directives": ["Не раскрывать хозяина."],
                  "revealConditions": ["Игрок сопоставил белые перья с тайной фракцией."],
                  "deceptionRisk": "high",
                  "isRevealed": false
                },
                {
                  "maskId": "mask_memory_beggar",
                  "displayName": "Нищий без памяти",
                  "publicArchetype": "сломанный странник",
                  "visiblePersonality": "просит помощи и боится Хранителей",
                  "concealedTruth": "Это бывший чиновник Сияющей Обители.",
                  "directives": ["Не спорить с сильными духами."],
                  "revealConditions": ["Игрок возвращает первый фрагмент памяти."],
                  "deceptionRisk": "medium",
                  "isRevealed": false
                }
              ],
              "soulDissipationTier": 1,
            """,
            StringComparison.Ordinal);
        await WriteProfileStateAsync(AppendRootProperties(json,
            """
          "afterlifeActorMaskAdds": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "mask": {
                "maskId": "mask_shining_senator",
                "displayName": "Сенатор Сияния",
                "publicArchetype": "благородный политик",
                "visiblePersonality": "говорит о порядке и долге",
                "concealedTruth": "Работает на скрытый круг Сарефа.",
                "directives": ["Продвигать решения Крыльев Ангелов."],
                "revealConditions": ["Игрок находит печать тайной ложи."],
                "deceptionRisk": "critical",
                "linkedSarefAgentId": "saref_agent_senator",
                "isRevealed": false,
                "updatedAtTurn": 61
              }
            }
          ],
          "afterlifeActorMaskUpdates": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "maskUpdate": {
                "maskId": "mask_memory_beggar",
                "visiblePersonality": "помнит обрывок голоса Сарефа",
                "deceptionRisk": "high",
                "updatedAtTurn": 62
              }
            }
          ],
          "afterlifeActorMaskRemovals": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "maskId": "mask_wings_emissary",
              "activeMaskId": "_true_self_"
            }
          ],
          "afterlifeActorActiveMaskChanges": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "activeMaskId": "_true_self_",
              "reason": "Игрок раскрыл роль посланника.",
              "evidence": "В сцене названа тайная ложа белых перьев.",
              "gmThoughtsSummary": "Хранитель больше не должен играть публичную маску.",
              "updatedAtTurn": 63
            }
          ]
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_entity_profile_mask_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_RemovingActiveMaskWithoutTrueSelf_ReportsContractIssue()
    {
        var json = BuildValidProfileJson().Replace(
            "\"soulDissipationTier\": 1,",
            """
              "activeMaskId": "mask_wings_emissary",
              "masks": [
                {
                  "maskId": "mask_wings_emissary",
                  "displayName": "Посланник Белых Перьев",
                  "publicArchetype": "учтивый посредник",
                  "visiblePersonality": "говорит мягко и уводит от имени Сарефа",
                  "concealedTruth": "Маска скрывает агента Крыльев Ангелов.",
                  "directives": ["Не раскрывать хозяина."],
                  "revealConditions": ["Игрок сопоставил белые перья с тайной фракцией."],
                  "deceptionRisk": "high",
                  "isRevealed": false
                }
              ],
              "soulDissipationTier": 1,
            """,
            StringComparison.Ordinal);
        await WriteProfileStateAsync(AppendRootProperties(json,
            """
          "afterlifeActorMaskRemovals": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "maskId": "mask_wings_emissary"
            }
          ]
        """));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_mask_remove_active_without_true_self", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PositiveRelationshipLockWithoutBreakthroughQuest_ReportsContractIssue()
    {
        await WriteProfileStateAsync(BuildValidProfileJson().Replace(
            "\"soulDissipationTier\": 1,",
            """
              "relationships": [
                {
                  "relationshipId": "guardian_mirror_player_trust",
                  "axis": "trust",
                  "targetActorType": "player_soul",
                  "targetActorId": "player_soul",
                  "value": 50,
                  "relationshipTier": "trust_breakthrough_required",
                  "relationshipLock": {
                    "lockState": "positive_locked",
                    "direction": "positive",
                    "threshold": 50,
                    "reason": "Хранитель требует испытание.",
                    "evidence": "Порог достигнут.",
                    "updatedAtTurn": 41
                  }
                }
              ],
              "soulDissipationTier": 1,
            """,
            StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_relationship_positive_lock_missing_breakthrough", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PositiveRelationshipThresholdWithoutGate_ReportsContractIssue()
    {
        await WriteProfileStateAsync(BuildValidProfileJson().Replace(
            "\"soulDissipationTier\": 1,",
            """
              "relationships": [
                {
                  "relationshipId": "guardian_mirror_player_trust",
                  "axis": "trust",
                  "targetActorType": "player_soul",
                  "targetActorId": "player_soul",
                  "value": 50,
                  "relationshipTier": "trusted"
                }
              ],
              "soulDissipationTier": 1,
            """,
            StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_relationship_positive_threshold_missing_gate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PointOfNoReturnWithoutProof_ReportsContractIssue()
    {
        await WriteProfileStateAsync(BuildValidProfileJson().Replace(
            "\"soulDissipationTier\": 1,",
            """
              "relationships": [
                {
                  "relationshipId": "guardian_mirror_player_debt",
                  "axis": "debt",
                  "targetActorType": "player_soul",
                  "targetActorId": "player_soul",
                  "value": -80,
                  "relationshipTier": "broken",
                  "relationshipLock": {
                    "lockState": "point_of_no_return",
                    "direction": "negative",
                    "threshold": -75,
                    "pointOfNoReturn": true,
                    "reason": "Хранитель считает долг невозвратным.",
                    "updatedAtTurn": 45
                  }
                }
              ],
              "soulDissipationTier": 1,
            """,
            StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_relationship_point_of_no_return_missing_proof", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_BreakthroughClearRequiresClearKeyword_ReportsContractIssue()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "resident",
              "actorId": "resident_liora",
              "displayName": "Лиора",
              "realm": "Shining Abode",
              "currencies": { "inkFeathers": 120, "lightSparks": 5 },
              "progression": { "enlightenment": { "experience": 48, "tier": 4 }, "radiance": { "experience": 80, "tier": 2 } },
              "standardArts": { "pressure": 1, "guard": 1 },
              "specialArts": [],
              "customStates": [],
              "fateCards": [],
              "relationships": [
                {
                  "relationshipId": "resident_liora_player_romance",
                  "axis": "romance",
                  "targetActorType": "player_soul",
                  "targetActorId": "player_soul",
                  "value": 50,
                  "relationshipTier": "romance_breakthrough_required",
                  "relationshipLock": {
                    "lockState": "positive_locked",
                    "direction": "positive",
                    "threshold": 50,
                    "breakthroughQuestId": "quest_liora_dawn_memory",
                    "reason": "Лиора требует сцену памяти.",
                    "evidence": "Порог близости достигнут.",
                    "updatedAtTurn": 50
                  }
                }
              ],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_liora", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ],
          "afterlifeBreakthroughQuestUpdates": [
            {
              "actorType": "resident",
              "actorId": "resident_liora",
              "relationshipId": "resident_liora_player_romance",
              "questId": "quest_liora_dawn_memory",
              "questType": "breakthrough",
              "status": "completed",
              "title": "Память рассветной клятвы",
              "sceneSummary": "Игрок завершил сцену.",
              "successCondition": "Память принята.",
              "evidence": "Сцена завершена.",
              "breakthroughQuestId": "quest_liora_dawn_memory",
              "gmThoughtsSummary": "GM пытается очистить гейт без _clear_.",
              "updatedAtTurn": 51
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_relationship_clear_requires_clear_keyword", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidAfterlifeActorAgency_PassesProfileValidation()
    {
        await WriteProfileStateAsync(BuildValidProfileWithAgencyJson());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_entity_profile_agency_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidUnlockedFateCard_PassesProfileValidation()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 120, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 48, "tier": 4 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": [],
              "customStates": [],
              "fateCards": [
                {
                  "cardId": "mirror_oath_returned",
                  "nameRu": "Возвращенная клятва",
                  "status": "unlocked",
                  "unlockConditions": {
                    "summary": "Душа прошла сцену суда и отказалась от удобной лжи."
                  },
                  "storyMeaning": "Хранитель возвращает часть утраченной памяти.",
                  "playerUnlocks": [
                    { "unlockId": "mirror_training", "summary": "Открывает тренировку Зеркальной Защиты." }
                  ],
                  "guardianEffects": [
                    { "effectId": "mirror_memory_restored", "summary": "Хранитель меняет стратегию и становится смелее." }
                  ],
                  "appliedAtTurn": 32,
                  "evidence": {
                    "summary": "Игрок завершил личное испытание Хранителя."
                  }
                }
              ],
              "soulDissipationTier": 1,
              "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_entity_profile_fate_card_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_LockedFateCardWithActiveUnlocks_ReportsContractIssue()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 120, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 48, "tier": 4 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": [],
              "customStates": [],
              "fateCards": [
                {
                  "cardId": "mirror_oath_returned",
                  "nameRu": "Возвращенная клятва",
                  "status": "locked",
                  "unlockConditions": {
                    "summary": "Сначала пройти сцену суда."
                  },
                  "storyMeaning": "Будущий потенциал Хранителя.",
                  "trainingUnlocks": [
                    { "unlockId": "mirror_training", "summary": "Еще не должен быть доступен." }
                  ]
                }
              ],
              "soulDissipationTier": 1,
              "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_fate_card_locked_effects_active", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FateCardUnlockWithoutEvidence_ReportsContractIssue()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 120, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 48, "tier": 4 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": [],
              "customStates": [],
              "fateCards": [
                {
                  "cardId": "mirror_oath_returned",
                  "nameRu": "Возвращенная клятва",
                  "status": "locked",
                  "unlockConditions": {
                    "summary": "Пройти сцену суда."
                  },
                  "storyMeaning": "Будущий потенциал Хранителя."
                }
              ],
              "soulDissipationTier": 1,
              "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ],
          "afterlifeFateCardUnlocks": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "cardId": "mirror_oath_returned",
              "appliedAtTurn": 32,
              "playerUnlocks": [
                { "unlockId": "mirror_training", "summary": "Открывает тренировку Зеркальной Защиты." }
              ]
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_fate_card_unlock_missing_evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentActivityWithoutQuestLink_ReportsContractIssue()
    {
        await WriteProfileStateAsync(BuildValidProfileWithAgencyJson()
            .Replace("\"linkedQuestId\": \"quest_mirror_oath_trial\"", "\"linkedQuestId\": \"quest_missing\"", StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_agency_activity_missing_quest_link", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentActivityWithoutCurrentGoal_ReportsContractIssue()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 120, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 48, "tier": 4 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 1,
              "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "personalQuests": [
                {
                  "questId": "quest_mirror_oath_trial",
                  "goalId": "goal_mirror_oath",
                  "title": "Суд зеркальной клятвы",
                  "status": "active",
                  "planSummary": "Подготовить испытание.",
                  "successCondition": "Душа осознанно откажется от лжи.",
                  "createdAtTurn": 31
                }
              ],
              "currentActivity": {
                "activityId": "activity_prepare_mirror_trial",
                "goalId": "goal_mirror_oath",
                "linkedQuestId": "quest_mirror_oath_trial",
                "activityType": "offscreen_preparation",
                "summary": "Собирает осколки свидетельств.",
                "status": "active",
                "gmThoughtsSummary": "Он готовит сцену, но не раскрывает причину.",
                "startedAtTurn": 31
              },
              "ledger": []
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_agency_activity_missing_quest_link", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AfterlifeActorActivityUpdateWithoutGmThoughts_ReportsContractIssue()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 120, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 48, "tier": 4 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 1,
              "progressionStrategy": { "strategyId": "strategy_guardian_mirror", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ],
          "afterlifeActorActivityUpdates": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "activityId": "activity_without_thoughts",
              "goalId": "goal_mirror_oath",
              "linkedQuestId": "quest_mirror_oath_trial",
              "activityType": "offscreen_preparation",
              "summary": "Готовит сцену без объяснения причины.",
              "status": "active",
              "startedAtTurn": 31
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_agency_activity_missing_gm_thoughts", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ChaosSeaProfileWithLightSparks_ReportsContractIssue()
    {
        await WriteProfileStateAsync(BuildValidProfileJson()
            .Replace("\"currencies\": { \"inkFeathers\": 120, \"lightSparks\": 0 }",
                "\"currencies\": { \"inkFeathers\": 120, \"lightSparks\": 3 }",
                StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_chaos_light_sparks_forbidden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SpecialArtOwnerMustMatchProfileIdentity()
    {
        await WriteProfileStateAsync(BuildValidProfileJson()
            .Replace("\"ownerActorType\": \"guardian\"", "\"ownerActorType\": \"player_soul\"", StringComparison.Ordinal)
            .Replace("\"ownerActorId\": \"guardian_mirror\"", "\"ownerActorId\": \"player_soul\"", StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_owner_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PlayerSoulIdentityMustNotBeUsedByNonPlayerProfile()
    {
        await WriteProfileStateAsync(BuildValidProfileJson()
            .Replace("\"actorId\": \"guardian_mirror\"", "\"actorId\": \"player_soul\"", StringComparison.Ordinal)
            .Replace("\"ownerActorId\": \"guardian_mirror\"", "\"ownerActorId\": \"player_soul\"", StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_player_identity_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_InvalidAfterlifeEntityProfile_ReportsContractIssues()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": -1, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 10, "tier": 1 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": { "pressure": 6 },
              "specialArts": [
                {
                  "artId": "mirror_guard",
                  "displayName": "Зеркальная Защита",
                  "ownerActorType": "guardian",
                  "ownerActorId": "guardian_mirror",
                  "baseOperation": "unknown_art",
                  "tier": 1,
                  "costMultiplierPercent": 100,
                  "upgradeCost": { "inkFeathers": 30, "lightSparks": 0 },
                  "canTeachPlayer": true,
                  "trainingConditions": [],
                  "effectSummary": "Отражает чужую защиту."
                }
              ],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать давление.", "priorityOrder": ["pressure"] },
              "ledger": []
            },
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Дубликат",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": {},
              "specialArts": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_2", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_negative_currency", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_invalid_standard_art_tier", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_invalid_special_art_base_operation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_invalid_special_art_cost_multiplier", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_missing_training_conditions", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_duplicate_actor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MalformedSpecialArtLearningReceipt_ReportsContractIssues()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": {},
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ],
          "afterlifeSpecialArtLearningReceipts": [
            {
              "receiptId": "",
              "teacherActorType": "guardian",
              "teacherActorId": "guardian_mirror",
              "artId": "",
              "playerActorId": "",
              "trainingConditionSatisfied": false,
              "roleplayEvidence": "",
              "summary": "",
              "initialTier": 5
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_missing_receipt_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_missing_art_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_missing_player_actor_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_condition_not_satisfied", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_missing_roleplay_evidence", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_invalid_initial_tier", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("{ }")]
    [InlineData("{ \"gold\": 100 }")]
    [InlineData("{ \"inkFeathers\": 0, \"lightSparks\": 0 }")]
    public async Task ValidateGameStateAsync_InvalidSpecialArtUpgradeCost_ReportsContractIssue(string upgradeCost)
    {
        await WriteProfileStateAsync(BuildValidProfileJson()
            .Replace("{ \"inkFeathers\": 30, \"lightSparks\": 0 }", upgradeCost, StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_invalid_upgrade_cost", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SpecialArtLearningReceiptWithoutAuthority_ReportsContractIssues()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": {},
              "specialArts": [
                {
                  "artId": "closed_mirror",
                  "displayName": "Закрытое Зеркало",
                  "ownerActorType": "guardian",
                  "ownerActorId": "guardian_mirror",
                  "baseOperation": "guard",
                  "tier": 1,
                  "costMultiplierPercent": 150,
                  "upgradeCost": { "inkFeathers": 30, "lightSparks": 0 },
                  "canTeachPlayer": false,
                  "effectSummary": "Не предназначено для обучения игрока."
                }
              ],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            },
            {
              "actorType": "player_soul",
              "actorId": "player_soul",
              "displayName": "Душа игрока",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": {},
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_player", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ],
          "afterlifeSpecialArtLearningReceipts": [
            {
              "receiptId": "learn_missing_art",
              "teacherActorType": "guardian",
              "teacherActorId": "guardian_mirror",
              "artId": "missing_art",
              "playerActorId": "player_soul",
              "learnedAtTurn": 12,
              "trainingConditionSatisfied": true,
              "roleplayEvidence": "Игрок прошёл сцену обучения.",
              "summary": "GM признал обучение."
            },
            {
              "receiptId": "learn_not_teachable",
              "teacherActorType": "guardian",
              "teacherActorId": "guardian_mirror",
              "artId": "closed_mirror",
              "playerActorId": "player_soul",
              "learnedAtTurn": 12,
              "trainingConditionSatisfied": true,
              "roleplayEvidence": "Игрок прошёл сцену обучения.",
              "summary": "GM признал обучение."
            },
            {
              "receiptId": "learn_unknown_teacher",
              "teacherActorType": "resident",
              "teacherActorId": "resident_missing",
              "artId": "closed_mirror",
              "playerActorId": "player_soul",
              "learnedAtTurn": 12,
              "trainingConditionSatisfied": true,
              "roleplayEvidence": "Игрок прошёл сцену обучения.",
              "summary": "GM признал обучение."
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_unknown_art", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_not_teachable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_special_art_learning_unknown_teacher", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MalformedAfterlifeEntityCustomStates_ReportContractIssues()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": {},
              "specialArts": [],
              "customStates": [
                {
                  "stateName": "Без идентификатора",
                  "currentValue": 1,
                  "minValue": 0,
                  "maxValue": 5,
                  "description": "Это состояние нельзя удалить без stateId.",
                  "progressionRule": { "changePerTurn": 0, "description": "Не меняется." },
                  "thresholds": []
                }
              ],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ],
          "afterlifeEntityCustomStateChanges": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "statesToAddOrUpdate": [
                {
                  "stateId": "bad_state",
                  "stateName": "Неполное состояние",
                  "currentValue": 1,
                  "minValue": 0,
                  "maxValue": 5,
                  "description": "Нет progressionRule и thresholds."
                }
              ],
              "statesToRemove": [""]
            },
            {
              "actorType": "resident",
              "actorId": "resident_echo"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_custom_state_missing_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_custom_state_missing_required_fields", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_custom_state_remove_invalid_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_custom_state_change_empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CustomStateChangeUnknownTarget_ReportsContractIssue()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": {},
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ],
          "afterlifeEntityCustomStateChanges": [
            {
              "actorType": "resident",
              "actorId": "resident_missing",
              "statesToRemove": ["old_state"]
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_custom_state_change_unknown_target", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_InvalidProfileCommandMarker_ReportsContractIssue()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": {},
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ],
          "lastInvalidProfileCommand": {
            "actorType": "resident",
            "actorId": "resident_missing",
            "statesToRemove": ["old_state"]
          },
          "lastInvalidProfileCommandReason": "unknown_custom_state_target"
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_command_invalid_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MalformedEntityProgressionLedgerAndOverride_ReportContractIssues()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": {},
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "progressionLedger": [
                {
                  "entryId": "",
                  "source": "client_auto_strategy",
                  "income": { "inkFeathers": -1 },
                  "spending": { "inkFeathers": 0 }
                }
              ],
              "ledger": []
            }
          ],
          "afterlifeEntityProgressionOverrides": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "cycleKey": "",
              "currencyDeltas": { "inkFeathers": -5 }
            },
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "cycleKey": "chaos:7",
              "reason": "Без дельт.",
              "summary": "Ничего не меняет."
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_ledger_missing_entry_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_ledger_missing_cycle_key", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_ledger_negative_amount", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_missing_cycle_key", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_missing_reason", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SplitProgressionOverrideLedger_PassesProfileValidation()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 5, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": { "pressure": 1 },
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать давление.", "priorityOrder": ["pressure"], "lastAutoProgressionCycleKey": "chaos:6" },
              "progressionLedger": [
                {
                  "entryId": "guardian_mirror_chaos_6_gm_override",
                  "cycleKey": "chaos:6",
                  "source": "gm_override",
                  "summary": "Хранитель потратил Перья в Море Хаоса.",
                  "income": { "inkFeathers": 0, "lightSparks": 0 },
                  "spending": { "inkFeathers": 5, "lightSparks": 0 }
                }
              ],
              "ledger": []
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_ledger_negative_amount", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_entity_profile_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_UnknownProgressionStrategyPriority_ReportsContractIssue()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": { "guard": 0 },
              "specialArts": [
                {
                  "artId": "mirror_guard",
                  "displayName": "Зеркальная Защита",
                  "ownerActorType": "guardian",
                  "ownerActorId": "guardian_mirror",
                  "baseOperation": "guard",
                  "tier": 1,
                  "costMultiplierPercent": 150,
                  "upgradeCost": { "inkFeathers": 30, "lightSparks": 0 },
                  "canTeachPlayer": true,
                  "trainingConditions": ["Провести сцену обучения с Хранителем Зеркал."],
                  "effectSummary": "При успехе отражает часть давления."
                }
              ],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать непонятное.", "priorityOrder": ["unknown_art"] },
              "ledger": []
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_strategy_unknown_priority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_UnknownProgressionStrategySpendCategory_ReportsContractIssue()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": { "guard": 0 },
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": {
                "strategyId": "strategy_1",
                "summary": "Качать непонятную категорию.",
                "priorityOrder": ["guard"],
                "allowedSpends": ["guard"],
                "forbiddenSpends": ["unknownCategory"]
              },
              "ledger": []
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_strategy_unknown_spend_category", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsProgressionOverrideForUnknownSpecialArt()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 100, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": { "guard": 0 },
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["guard"] },
              "ledger": []
            }
          ],
          "afterlifeEntityProgressionOverrides": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "cycleKey": "chaos:9",
              "reason": "GM override.",
              "summary": "Опечатка в artId.",
              "specialArtTierDeltas": { "miror_guard": 1 }
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_unknown_special_art", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_RejectsInvalidProgressionOverrideMarker()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [],
          "lastInvalidProgressionOverride": {
            "actorType": "guardian",
            "actorId": "guardian_mirror",
            "specialArtTierDeltas": { "miror_guard": 1 }
          },
          "lastInvalidProgressionOverrideReason": "unknown_special_art"
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_invalid_authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MalformedSpecialArtAndSoulDissipationOverrides_ReportContractIssues()
    {
        await WriteProfileStateAsync("""
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "currencies": { "inkFeathers": 0, "lightSparks": 0 },
              "progression": { "enlightenment": { "experience": 0, "tier": 0 }, "radiance": { "experience": 0, "tier": 0 } },
              "standardArts": { "guard": 0 },
              "specialArts": [
                {
                  "artId": "mirror_guard",
                  "displayName": "Зеркальная Защита",
                  "ownerActorType": "guardian",
                  "ownerActorId": "guardian_mirror",
                  "baseOperation": "guard",
                  "tier": 1,
                  "costMultiplierPercent": 150,
                  "upgradeCost": { "inkFeathers": 30, "lightSparks": 0 },
                  "canTeachPlayer": true,
                  "trainingConditions": ["Провести сцену обучения с Хранителем Зеркал."],
                  "effectSummary": "При успехе отражает часть давления."
                }
              ],
              "customStates": [],
              "soulDissipationTier": 0,
              "progressionStrategy": { "strategyId": "strategy_1", "summary": "Качать защиту.", "priorityOrder": ["mirror_guard", "soul_dissipation"] },
              "ledger": []
            }
          ],
          "afterlifeEntityProgressionOverrides": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "cycleKey": "chaos:7",
              "reason": "Некорректное изменение.",
              "summary": "Неверные поля.",
              "specialArtTierDeltas": [],
              "soulDissipationTierDelta": "up"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_invalid_special_art_delta", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_entity_profile_progression_override_invalid_soul_dissipation_delta", StringComparison.OrdinalIgnoreCase));
    }

    private Task WriteProfileStateAsync(string json) =>
        _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, json);

    private static string AppendRootProperties(string json, string rootProperties)
    {
        var insertionIndex = json.LastIndexOf("\n}", StringComparison.Ordinal);
        if (insertionIndex < 0)
            throw new InvalidOperationException("Test JSON root closing brace was not found.");

        return json.Insert(insertionIndex, ",\n" + rootProperties.TrimEnd());
    }

    private static string BuildCurrentProfileUpdateWithSpecialArt(string specialArtPayload) =>
        $$"""
        {
          "schemaVersion": 1,
          "afterlifeEntityProfileUpdates": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "locationName": "Зеркальная Обитель",
              "currencies": { "inkFeathers": 120, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 48, "tier": 4 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": [
                {
                  "artId": "mirror_guard",
                  "displayName": "Зеркальная Защита",
                  "ownerActorType": "guardian",
                  "ownerActorId": "guardian_mirror",
                  "baseOperation": "guard",
                  "tier": 1,
                  "costMultiplierPercent": 150,
                  "upgradeCost": { "inkFeathers": 30, "lightSparks": 0 },
                  "canTeachPlayer": true,
                  "trainingConditions": ["Провести сцену обучения с Хранителем Зеркал."],
        {{specialArtPayload}}
                }
              ],
              "customStates": [],
              "fateCards": [],
              "soulDissipationTier": 1,
              "progressionStrategy": {
                "strategyId": "strategy_guardian_mirror",
                "summary": "Сначала укрепляет защиту, затем давление.",
                "priorityOrder": ["guard", "pressure"],
                "lastUpdatedAtTurn": 22
              },
              "warnings": [],
              "ledger": []
            }
          ]
        }
        """;

    private static string BuildValidProfileJson() =>
        """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "locationName": "Зеркальная Обитель",
              "currencies": { "inkFeathers": 120, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 48, "tier": 4 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": [
                {
                  "artId": "mirror_guard",
                  "displayName": "Зеркальная Защита",
                  "ownerActorType": "guardian",
                  "ownerActorId": "guardian_mirror",
                  "baseOperation": "guard",
                  "tier": 1,
                  "costMultiplierPercent": 150,
                  "upgradeCost": { "inkFeathers": 30, "lightSparks": 0 },
                  "canTeachPlayer": true,
                  "trainingConditions": ["Провести сцену обучения с Хранителем Зеркал."],
                  "effectSummary": "При успехе отражает часть давления в сторону противника."
                }
              ],
              "soulDissipationTier": 1,
              "progressionStrategy": {
                "strategyId": "strategy_guardian_mirror",
                "summary": "Сначала укрепляет защиту, затем давление.",
                "priorityOrder": ["guard", "pressure"],
                "lastUpdatedAtTurn": 22
              },
              "warnings": ["ОПАСНО: может развеять душу после победы, если решит это сделать."],
              "ledger": [
                {
                  "entryId": "profile_ledger_001",
                  "turnNumber": 22,
                  "reason": "initial_profile",
                  "summary": "Профиль создан при встрече с хранителем."
                }
              ]
            }
          ]
        }
        """;

    private static string BuildValidProfileWithAgencyJson() =>
        """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "actorType": "guardian",
              "actorId": "guardian_mirror",
              "displayName": "Хранитель Зеркал",
              "realm": "Chaos Sea",
              "locationName": "Зеркальная Обитель",
              "currencies": { "inkFeathers": 120, "lightSparks": 0 },
              "progression": {
                "enlightenment": { "experience": 48, "tier": 4 },
                "radiance": { "experience": 0, "tier": 0 }
              },
              "standardArts": { "pressure": 2, "guard": 1 },
              "specialArts": [],
              "customStates": [],
              "soulDissipationTier": 1,
              "progressionStrategy": {
                "strategyId": "strategy_guardian_mirror",
                "summary": "Сначала укрепляет защиту, затем давление.",
                "priorityOrder": ["guard", "pressure"]
              },
              "goals": {
                "goalId": "goal_mirror_oath",
                "shortTermGoal": "Проверить, понимает ли Душа цену клятв.",
                "longTermGoal": "Не дать Сарефу снова использовать забытые обеты.",
                "plan": "Подтолкнуть Душу к сцене зеркального суда.",
                "gmThoughtsSummary": "Хранитель действует из страха повторить старую ошибку.",
                "updatedAtTurn": 31
              },
              "personalQuests": [
                {
                  "questId": "quest_mirror_oath_trial",
                  "goalId": "goal_mirror_oath",
                  "title": "Суд зеркальной клятвы",
                  "status": "active",
                  "planSummary": "Подготовить испытание и не раскрывать истинную причину заранее.",
                  "successCondition": "Душа осознанно откажется от удобной лжи.",
                  "createdAtTurn": 31
                }
              ],
              "currentActivity": {
                "activityId": "activity_prepare_mirror_trial",
                "goalId": "goal_mirror_oath",
                "linkedQuestId": "quest_mirror_oath_trial",
                "activityType": "offscreen_preparation",
                "summary": "Собирает осколки свидетельств для сцены суда.",
                "status": "active",
                "gmThoughtsSummary": "Он готовит сцену, но не принуждает игрока идти туда.",
                "startedAtTurn": 31
              },
              "ledger": []
            }
          ]
        }
        """;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Ignore temp cleanup failures.
        }
    }
}
