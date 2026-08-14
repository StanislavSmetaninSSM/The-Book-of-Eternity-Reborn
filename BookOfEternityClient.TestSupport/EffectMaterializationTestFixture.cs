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
        if (!string.Equals(profile, "periodic_damage", StringComparison.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported test profile.");

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
            ["allowedRealms"] = new JsonArray("mortal_world"),
            ["allowedTargetKinds"] = new JsonArray("player", "npc", "combatant"),
            ["components"] = new JsonArray(CreatePeriodicDamageComponent()),
            ["parameterBounds"] = new JsonObject
            {
                ["amount"] = new JsonObject
                {
                    ["kind"] = "number",
                    ["minimum"] = 1,
                    ["maximum"] = 10
                }
            },
            ["stacking"] = new JsonObject
            {
                ["stackKey"] = "bleeding",
                ["policy"] = "stack",
                ["maxStacks"] = 3,
                ["atMaximum"] = "no_change",
                ["refreshMode"] = null,
                ["mergeRule"] = null
            },
            ["lifetime"] = new JsonObject
            {
                ["mode"] = "turns",
                ["initialTurns"] = 3,
                ["advancePhase"] = "owner_turn_end"
            },
            ["triggers"] = new JsonArray(CreateTurnEndTrigger()),
            ["removal"] = CreateRemoval(),
            ["links"] = new JsonArray(new JsonObject
            {
                ["kind"] = "wound",
                ["targetId"] = "wound_test_torn_side",
                ["role"] = "source"
            })
        };
    }

    internal static JsonObject CreateApplyCommand(string targetKind = "player")
    {
        var targetId = targetKind switch
        {
            "player" => "player_current",
            "npc" => "npc_test_healer",
            "combatant" => CombatantId,
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

    internal static JsonObject CreateCanonicalEffect(string ownerKind = "player")
    {
        var targetId = ownerKind switch
        {
            "player" => "player_current",
            "npc" => "npc_test_healer",
            "combatant" => CombatantId,
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
            ["components"] = new JsonArray(CreatePeriodicDamageComponent()),
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
            ["links"] = new JsonArray(new JsonObject
            {
                ["kind"] = "wound",
                ["targetId"] = "wound_test_torn_side",
                ["role"] = "source"
            }),
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
            var ownerKind = target["kind"]!.GetValue<string>();
            var ownerId = target["targetId"]!.GetValue<string>();
            var carrier = ResolveCarrier(ownerKind);

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
                    ["targetKind"] = ownerKind,
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
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind), ownerKind, "Unsupported test owner.")
        };
}
