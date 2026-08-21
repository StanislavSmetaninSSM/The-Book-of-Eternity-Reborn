using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

internal static class EffectMaterializationTestFixture
{
    internal const string DefinitionKey = "bleeding_consequence";
    internal const string EffectId = "effect_test_bleeding";
    internal const string TransitionId = "effect_transition_test_apply";
    internal const string CombatantRef = "combatant_ref_test_raider";
    internal const string CombatantId = "combatant_test_raider";

    internal static JsonObject CreateDefinition(string profile = "periodic_damage")
    {
        var isAfterlifeCondition = string.Equals(
            profile,
            "afterlife_combat_condition",
            StringComparison.Ordinal);
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitionKey"] = DefinitionKey,
            ["display"] = new JsonObject
            {
                ["name"] = "Кровотечение",
                ["description"] = "Рана продолжает отнимать силы.",
                ["category"] = "debuff",
                ["visibility"] = "visible"
            },
            ["allowedRealms"] = isAfterlifeCondition
                ? new JsonArray("chaos_sea")
                : new JsonArray("mortal_world"),
            ["allowedTargetKinds"] = isAfterlifeCondition
                ? new JsonArray("spiritual_conflict_side")
                : new JsonArray("player", "npc", "combatant"),
            ["components"] = new JsonArray(CreateComponent(profile)),
            ["parameterBounds"] = CreateParameterBounds(profile),
            ["stacking"] = new JsonObject
            {
                ["stackKey"] = "bleeding",
                ["policy"] = "stack",
                ["maxStacks"] = 3,
                ["atMaximum"] = "no_change",
                ["refreshMode"] = null,
                ["mergeRule"] = null
            },
            ["lifetime"] = isAfterlifeCondition
                ? new JsonObject
                {
                    ["mode"] = "turns",
                    ["initialTurns"] = 3,
                    ["advancePhase"] = "afterlife_exchange_end"
                }
                : new JsonObject
                {
                    ["mode"] = "turns",
                    ["initialTurns"] = 3,
                    ["advancePhase"] = "owner_turn_end"
                },
            ["triggers"] = new JsonArray(CreateTurnEndTrigger()),
            ["removal"] = CreateRemoval(),
            ["links"] = CreateLinks(profile)
        };
    }

    internal static JsonObject CreateApplyCommand(string targetKind = "player")
    {
        var targetId = targetKind switch
        {
            "player" => "player_current",
            "npc" => "npc_test_healer",
            "combatant" => CombatantId,
            "guardian" or "resident" or "radiant_actor" or "afterlife_actor" =>
                "afterlife_actor_test",
            _ => throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, "Unsupported test target.")
        };

        return new JsonObject
        {
            ["operation"] = "apply",
            ["target"] = new JsonObject
            {
                ["kind"] = targetKind,
                ["targetId"] = targetId
            },
            ["source"] = new JsonObject
            {
                ["kind"] = "wound",
                ["sourceId"] = "wound_test_torn_side",
                ["definitionKey"] = DefinitionKey
            },
            ["parameters"] = new JsonObject
            {
                ["amount"] = 3
            },
            ["eventRef"] = new JsonObject
            {
                ["kind"] = "accepted_turn",
                ["authorityId"] = "turn_42"
            },
            ["reason"] = "Рана снова открылась."
        };
    }

    internal static JsonObject CreateCanonicalEffect(
        string ownerKind = "player",
        string profile = "periodic_damage")
    {
        var targetId = ownerKind switch
        {
            "player" => "player_current",
            "npc" => "npc_test_healer",
            "combatant" => CombatantId,
            "guardian" or "resident" or "radiant_actor" or "afterlife_actor" =>
                "afterlife_actor_test",
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind), ownerKind, "Unsupported test owner.")
        };

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entityKind"] = "active_effect",
            ["effectId"] = EffectId,
            ["state"] = "active",
            ["realm"] = "mortal_world",
            ["target"] = new JsonObject
            {
                ["kind"] = ownerKind,
                ["targetId"] = targetId
            },
            ["display"] = new JsonObject
            {
                ["name"] = "Кровотечение",
                ["description"] = "Рана продолжает отнимать силы.",
                ["category"] = "debuff",
                ["visibility"] = "visible",
                ["sourceLabel"] = "Рваная рана"
            },
            ["source"] = new JsonObject
            {
                ["kind"] = "wound",
                ["sourceId"] = "wound_test_torn_side",
                ["definitionKey"] = DefinitionKey
            },
            ["components"] = new JsonArray(CreateComponent(profile)),
            ["lifetime"] = new JsonObject
            {
                ["mode"] = "turns",
                ["remainingTurns"] = 3,
                ["advancePhase"] = "owner_turn_end",
                ["displayText"] = "Ещё три хода владельца"
            },
            ["stacking"] = new JsonObject
            {
                ["stackKey"] = "bleeding",
                ["policy"] = "stack",
                ["maxStacks"] = 3,
                ["currentStacks"] = 1,
                ["refreshMode"] = null,
                ["mergeRule"] = null
            },
            ["triggers"] = new JsonArray(CreateTurnEndTrigger()),
            ["removal"] = CreateRemoval(),
            ["links"] = CreateLinks(profile),
            ["chronology"] = new JsonObject
            {
                ["createdAtTurn"] = 42,
                ["createdEventRef"] = "turn_42:wound_opened",
                ["lastTransitionId"] = TransitionId,
                ["lastTransitionTurn"] = 42
            }
        };
    }

    internal static JsonObject CreateIdentityIndex(params JsonObject[] effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        var entries = new JsonArray();
        foreach (var sourceEffect in effects)
        {
            ArgumentNullException.ThrowIfNull(sourceEffect);
            var effect = sourceEffect.DeepClone().AsObject();
            var target = effect["target"]!.DeepClone().AsObject();
            var source = effect["source"]!.DeepClone().AsObject();
            var stacking = effect["stacking"]!.AsObject();
            var targetKind = target["kind"]!.GetValue<string>();
            var ownerKind = targetKind;
            var ownerId = target["targetId"]!.GetValue<string>();
            var carrier = ResolveCarrier(targetKind);

            entries.Add(new JsonObject
            {
                ["effectId"] = effect["effectId"]!.GetValue<string>(),
                ["state"] = effect["state"]!.GetValue<string>(),
                ["realm"] = effect["realm"]!.GetValue<string>(),
                ["owner"] = new JsonObject
                {
                    ["kind"] = ownerKind,
                    ["ownerId"] = ownerId,
                    ["carrierPath"] = carrier.Path,
                    ["collection"] = carrier.Collection
                },
                ["target"] = target,
                ["source"] = source.DeepClone(),
                ["stackCoordinate"] = new JsonObject
                {
                    ["realm"] = effect["realm"]!.GetValue<string>(),
                    ["targetKind"] = targetKind,
                    ["targetId"] = ownerId,
                    ["sourceKind"] = source["kind"]!.GetValue<string>(),
                    ["sourceId"] = source["sourceId"]!.GetValue<string>(),
                    ["stackKey"] = stacking["stackKey"]!.GetValue<string>()
                },
                ["createdAtTurn"] = 42,
                ["transitions"] = new JsonArray(CreateApplyTransition(
                    effect["effectId"]!.GetValue<string>()))
            });
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = entries
        };
    }

    internal static JsonObject CreateCommandRoot(params JsonObject[] changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var effectChanges = new JsonArray();
        foreach (var change in changes)
        {
            ArgumentNullException.ThrowIfNull(change);
            effectChanges.Add(change.DeepClone());
        }

        return new JsonObject
        {
            ["effectChanges"] = effectChanges,
            ["effectResolutionReceipts"] = new JsonArray()
        };
    }

    internal static JsonObject CreateSameTurnMortalActor(string initialId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initialId);
        var actor = MortalActorTestFixtures.CreateActor(initialId);
        actor["NPCId"] = null;
        actor["initialId"] = initialId;
        actor["personalityTraits"] = new JsonArray(
            CreatePersonalityTrait(
                "Внимательность",
                "Замечает расхождения в записях.",
                "Очень внимателен",
                8),
            CreatePersonalityTrait(
                "Осторожность",
                "Проверяет каждое свидетельство дважды.",
                "Осторожен",
                7),
            CreatePersonalityTrait(
                "Последовательность",
                "Не меняет вывод без новых доказательств.",
                "Последователен",
                6));
        actor["materialization"] = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = $"mat_{initialId}_turn_42",
            ["actorType"] = "mortal_npc",
            ["actorId"] = initialId,
            ["materializedAtTurn"] = 42,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["canFight"] = false,
                ["canTeach"] = true,
                ["canTrade"] = false,
                ["ownsItems"] = false
            },
            ["sections"] = new JsonObject
            {
                ["skills"] = EmptyDisposition("Этот NPC не использует активные или пассивные навыки."),
                ["inventory"] = EmptyDisposition("Этот NPC не носит личных предметов."),
                ["fateCards"] = EmptyDisposition("Карта судьбы ещё не открыта."),
                ["personalQuests"] = EmptyDisposition("Личная просьба пока не сформировалась."),
                ["relationships"] = new JsonObject { ["state"] = "populated" }
            }
        };
        return actor;
    }

    internal static JsonObject CreateSameTurnMortalFaction(string initialId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initialId);
        return new JsonObject
        {
            ["factionId"] = null,
            ["initialId"] = initialId,
            ["isNewFaction"] = true,
            ["name"] = "Орден точного следа",
            ["description"] = "Небольшое братство, сохраняющее точную историю событий.",
            ["image_prompt"] = "weathered archivists beneath a dark stone arch, realistic lighting",
            ["factionColor"] = "#6B627A",
            ["purpose"] = "Сохранять точные связи между причинами и последствиями.",
            ["currentAgenda"] = "Проверить записи последнего перехода.",
            ["principles"] = new JsonArray("Ни одна связь не выводится из догадки."),
            ["memory"] = new JsonObject
            {
                ["summary"] = "Орден возник вокруг старого архива.",
                ["lastUpdatedTurn"] = 42,
                ["enduringFacts"] = new JsonArray("Архив хранит только проверенные записи."),
                ["openThreads"] = new JsonArray("Источник последней аномалии неизвестен.")
            },
            ["governance"] = new JsonObject
            {
                ["model"] = "Совет хранителей",
                ["decisionProcess"] = "Решение принимается после сверки записей."
            },
            ["leadership"] = new JsonObject
            {
                ["leadershipState"] = "vacant",
                ["summary"] = "Пост главы пока свободен.",
                ["leaderNpcIds"] = new JsonArray()
            },
            ["powerProfile"] = new JsonObject
            {
                ["military"] = 0,
                ["economic"] = 0,
                ["social"] = 0,
                ["covert"] = 0,
                ["logistics"] = 0,
                ["stability"] = 0,
                ["arcane_tech"] = 0,
                ["exploration"] = 0
            },
            ["ranks"] = new JsonObject { ["branches"] = new JsonArray() },
            ["structuredBonuses"] = new JsonArray(),
            ["resources"] = new JsonObject
            {
                ["metaResources"] = new JsonArray(),
                ["strategicGoods"] = new JsonArray()
            },
            ["relations"] = new JsonArray(),
            ["activeProjects"] = new JsonArray(),
            ["completedProjects"] = new JsonArray(),
            ["controlledTerritories"] = new JsonArray(),
            ["customStates"] = new JsonArray(),
            ["scribeChronicle"] = new JsonArray("#42 - Орден закрепил первую запись."),
            ["isPlayerFaction"] = false,
            ["isPlayerMember"] = false,
            ["playerRank"] = null,
            ["playerBranch"] = null,
            ["playerStrategyDirective"] = null,
            ["reputation"] = 0,
            ["reputationDescription"] = null,
            ["level"] = 1,
            ["experience"] = 0,
            ["experienceForNextLevel"] = 100,
            ["developmentArchetype"] = "Custodian",
            ["activeEffectDefinitions"] = new JsonArray(CreateDefinition()),
            ["materialization"] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["materializationId"] = $"fmat_{initialId}_turn_42",
                ["factionType"] = "mortal_faction",
                ["factionId"] = initialId,
                ["materializedAtTurn"] = 42,
                ["state"] = "complete",
                ["capabilities"] = new JsonObject
                {
                    ["hasFormalHierarchy"] = false,
                    ["usesFactionResources"] = false,
                    ["maintainsRelations"] = false,
                    ["runsProjects"] = false,
                    ["holdsTerritoryOrInfluence"] = false,
                    ["supportsPlayerMembership"] = false,
                    ["usesCustomMechanics"] = false
                },
                ["sections"] = new JsonObject
                {
                    ["hierarchy"] = EmptyDisposition("Формальных рангов пока нет."),
                    ["resources"] = EmptyDisposition("Формальных ресурсов пока нет."),
                    ["relations"] = EmptyDisposition("Формальных отношений пока нет."),
                    ["projects"] = EmptyDisposition("Проектов пока нет."),
                    ["territoryAndInfluence"] = EmptyDisposition("Территория не заявлена."),
                    ["playerMembership"] = EmptyDisposition("Игрок не состоит во фракции."),
                    ["customStates"] = EmptyDisposition("Особых состояний пока нет.")
                }
            }
        };
    }

    internal static JsonObject CreateSameTurnCombatant(string combatantRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(combatantRef);
        return new JsonObject
        {
            ["combatantRef"] = combatantRef,
            ["NPCId"] = null,
            ["name"] = "Налётчик",
            ["image_prompt"] = "dark fantasy roadside raider, realistic lighting",
            ["description"] = "Одинокий налётчик с изношенным клинком.",
            ["type"] = "humanoid",
            ["isGroup"] = false,
            ["maxHealth"] = "100%",
            ["maxPoise"] = "100%",
            ["currentHealth"] = "100%",
            ["currentPoise"] = "100%",
            ["initiative"] = 17,
            ["actions"] = new JsonArray(),
            ["resistances"] = new JsonArray(),
            ["activeBuffs"] = new JsonArray(),
            ["activeDebuffs"] = new JsonArray()
        };
    }

    internal static JsonObject CreatePendingResolutionRoot() =>
        new()
        {
            ["schemaVersion"] = 1,
            ["sessionId"] = "session_effect_materialization_test",
            ["requests"] = new JsonArray(new JsonObject
            {
                ["requestId"] = "effect_resolution_test",
                ["effectId"] = EffectId,
                ["target"] = new JsonObject
                {
                    ["kind"] = "player",
                    ["targetId"] = "player_current"
                },
                ["source"] = new JsonObject
                {
                    ["kind"] = "wound",
                    ["sourceId"] = "wound_test_torn_side",
                    ["definitionKey"] = DefinitionKey
                },
                ["triggerId"] = "on_owner_turn_end",
                ["eventRef"] = "turn_42:end",
                ["allowedResultKinds"] = new JsonArray("narrated_no_state_change"),
                ["allowedTargetPaths"] = new JsonArray(),
                ["numericBounds"] = new JsonObject(),
                ["requiredCompanions"] = new JsonArray(),
                ["fullTurnResubmissionRequired"] = true
            })
        };

    private static JsonObject CreatePeriodicDamageComponent() =>
        new()
        {
            ["componentId"] = "component_001",
            ["profile"] = "periodic_damage",
            ["priority"] = 100,
            ["payload"] = new JsonObject
            {
                ["resource"] = "health",
                ["amount"] = 3,
                ["damageType"] = "bleeding",
                ["floorPolicy"] = "registered_resource_floor"
            }
        };

    private static JsonObject CreateComponent(string profile) =>
        profile switch
        {
            "characteristic_modifier" => CreateProfileComponent(profile, new JsonObject
            {
                ["characteristic"] = "dexterity",
                ["operation"] = "flat",
                ["value"] = -2
            }),
            "roll_modifier" => CreateProfileComponent(profile, new JsonObject
            {
                ["operations"] = new JsonArray("attack_roll"),
                ["contribution"] = "disadvantage"
            }),
            "resistance_modifier" => CreateProfileComponent(profile, new JsonObject
            {
                ["resistance"] = "bleeding",
                ["operation"] = "flat",
                ["value"] = -1,
                ["cap"] = new JsonObject
                {
                    ["minimum"] = -100,
                    ["maximum"] = 100
                }
            }),
            "periodic_damage" => CreatePeriodicDamageComponent(),
            "periodic_restore" => CreateProfileComponent(profile, new JsonObject
            {
                ["resource"] = "health",
                ["amount"] = 3,
                ["capPolicy"] = "registered_resource_cap"
            }),
            "action_control" => CreateProfileComponent(profile, new JsonObject
            {
                ["action"] = "movement",
                ["operation"] = "restrict"
            }),
            "event_reaction" => CreateProfileComponent(profile, new JsonObject
            {
                ["eventType"] = "owner_damaged",
                ["resultKind"] = "apply_definition",
                ["definitionKey"] = DefinitionKey,
                ["dependency"] = "after_current_event"
            }),
            "wound_consequence" => CreateProfileComponent(profile, new JsonObject
            {
                ["woundId"] = "wound_test_torn_side",
                ["symptom"] = "bleeding",
                ["consequence"] = "periodic_damage"
            }),
            "afterlife_combat_condition" => CreateProfileComponent(profile, new JsonObject
            {
                ["conditionKind"] = "burden",
                ["targetSide"] = "opposition",
                ["actorId"] = "afterlife_actor_test",
                ["operations"] = new JsonArray("pressure"),
                ["axes"] = new JsonArray("rollMode"),
                ["counterplay"] = new JsonArray("purification"),
                ["payoff"] = "attrition"
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported test profile.")
        };

    private static JsonObject CreateProfileComponent(string profile, JsonObject payload) =>
        new()
        {
            ["componentId"] = "component_001",
            ["profile"] = profile,
            ["priority"] = 100,
            ["payload"] = payload
        };

    private static JsonObject CreateParameterBounds(string profile) =>
        profile switch
        {
            "characteristic_modifier" or "resistance_modifier" => new JsonObject
            {
                ["value"] = CreateNumericBound(-100, 100)
            },
            "periodic_damage" or "periodic_restore" => new JsonObject
            {
                ["amount"] = CreateNumericBound(1, 10)
            },
            "afterlife_combat_condition" or "roll_modifier" or "action_control" or
                "event_reaction" or "wound_consequence" =>
                new JsonObject(),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported test profile.")
        };

    private static JsonObject CreateNumericBound(int minimum, int maximum) =>
        new()
        {
            ["kind"] = "number",
            ["minimum"] = minimum,
            ["maximum"] = maximum
        };

    private static JsonObject CreateTurnEndTrigger() =>
        new()
        {
            ["triggerId"] = "on_owner_turn_end",
            ["eventType"] = "owner_turn_end",
            ["priority"] = 100,
            ["componentIds"] = new JsonArray("component_001"),
            ["consumeUses"] = false,
            ["resolutionMode"] = "deterministic"
        };

    private static JsonObject CreateRemoval() =>
        new()
        {
            ["dispelCategories"] = new JsonArray("physical_treatment"),
            ["cureKinds"] = new JsonArray("stop_bleeding"),
            ["onSourceLoss"] = "expire",
            ["onConditionLoss"] = null,
            ["manualAuthorities"] = new JsonArray()
        };

    private static JsonArray CreateLinks(string profile) =>
        string.Equals(profile, "wound_consequence", StringComparison.Ordinal)
            ? new JsonArray(new JsonObject
            {
                ["kind"] = "wound",
                ["targetId"] = "wound_test_torn_side",
                ["role"] = "source"
            })
            : new JsonArray();

    private static JsonObject EmptyDisposition(string reason) =>
        new()
        {
            ["state"] = "empty_by_design",
            ["reason"] = reason
        };

    private static JsonObject CreatePersonalityTrait(
        string name,
        string description,
        string valueDescription,
        int value) =>
        new()
        {
            ["traitName"] = name,
            ["description"] = description,
            ["valueDescription"] = valueDescription,
            ["value"] = value
        };

    private static JsonObject CreateApplyTransition(string effectId) =>
        new()
        {
            ["transitionId"] = TransitionId,
            ["kind"] = "create",
            ["turn"] = 42,
            ["eventRef"] = "turn_42:wound_opened",
            ["sourceEffectIds"] = new JsonArray(),
            ["resultEffectIds"] = new JsonArray(effectId),
            ["receiptId"] = null
        };

    private static (string Path, string Collection) ResolveCarrier(string ownerKind) =>
        ownerKind switch
        {
            "player" => ("game_state/player/effects.json", "activeEffects"),
            "npc" => ("game_state/npcs/npc_effects.json", "activeEffects"),
            "combatant" => ("game_state/combat/enemies.json", "activeDebuffs"),
            "guardian" or "resident" or "radiant_actor" or "afterlife_actor" => (
                "game_state/meta/afterlife_entity_profiles.json",
                "activeEffects"),
            "spiritual_conflict_side" => (
                "game_state/meta/afterlife_spiritual_conflict_state.json",
                "combatConditions"),
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind), ownerKind, "Unsupported test owner.")
        };
}
