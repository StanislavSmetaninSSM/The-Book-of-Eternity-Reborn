using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

/// <summary>Strict, deterministic version-1 JSON data for future wound contract tests.</summary>
internal static class WoundContractTestData
{
    internal static JsonArray CreateRootBoundReactionComplicationDefinitions(
        string targetPolicy)
    {
        var producer = WoundContractTestData.CreateApplyDefinitionRoot(
            "wound_test_torn_side",
            "mortal_world",
            "irritation_reaction",
            "irritation_reaction_target");
        producer["links"] = new JsonArray();
        var target = WoundContractTestData.CreateOwnedEffectDefinition(
            "wound_test_torn_side",
            "mortal_world",
            "irritation_reaction_target",
            "action_control");
        target["links"] = new JsonArray();
        target["stacking"]!["policy"] = targetPolicy;

        return new JsonArray(
            new JsonObject
            {
                ["definitionRef"] = "irritation_reaction",
                ["definition"] = producer,
                ["root"] = new JsonObject
                {
                    ["ownership"] = new JsonObject
                    {
                        ["kind"] = "complication",
                        ["complicationRef"] = "irritation"
                    },
                    ["slots"] = new JsonArray(new JsonObject
                    {
                        ["profileKey"] = "event_reaction",
                        ["readableSummary"] = "Боль усиливает следующий связанный эффект."
                    })
                }
            },
            new JsonObject
            {
                ["definitionRef"] = "irritation_reaction_target",
                ["definition"] = target,
                ["root"] = new JsonObject
                {
                    ["ownership"] = new JsonObject
                    {
                        ["kind"] = "complication",
                        ["complicationRef"] = "irritation"
                    },
                    ["slots"] = new JsonArray(new JsonObject
                    {
                        ["profileKey"] = "action_control",
                        ["readableSummary"] = "Боль ограничивает движение."
                    })
                }
            });
    }

    private static readonly string[] DistinctMortalProfiles =
    {
        "action_control",
        "characteristic_modifier",
        "resistance_modifier",
        "periodic_damage",
        "roll_modifier"
    };

    internal const int ActiveWoundLimit = 2_000;
    internal const int HistoryRowLimit = 20_000;
    internal const int CommandLimit = 128;
    internal const int PendingCandidateLimit = 64;
    internal const int TreatmentPathLimit = 32;
    internal const int DiagnosisPathLimit = 32;
    internal const int RequirementLimit = 16;
    internal const int ComplicationLimit = 16;
    internal const int ConsequenceLimit = 4;
    internal const int OwnedEffectDefinitionLimit = 5;
    internal const int OwnedEffectRootBindingLimit = 5;
    internal const int TransitionsPerAcceptedTurnLimit = 32;

    internal static JsonObject CreatePlayerCarrier(params JsonObject[] activeWounds) => new()
    {
        ["schemaVersion"] = 1,
        ["owner"] = CreatePlayerCarrierOwner(),
        ["activeWounds"] = CloneArray(activeWounds)
    };

    internal static JsonObject CreateNamedNpcCarrier(
        string npcId = "exact-existing-npc-id",
        params JsonObject[] activeWounds) => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray(new JsonObject
        {
            ["npcId"] = npcId,
            ["activeWounds"] = CloneArray(activeWounds)
        })
    };

    /// <summary>
    /// Clones an existing combat root and writes only at the caller-selected JSON pointer.
    /// The pointer identifies a combatant or a group member; no name lookup is performed.
    /// </summary>
    internal static JsonObject CreateCombatantCarrier(
        JsonObject combatRoot,
        string selectedCombatantPointer,
        params JsonObject[] activeWounds) =>
        AddActiveWoundsAtPointer(combatRoot, selectedCombatantPointer, activeWounds);

    /// <summary>
    /// Clones an existing afterlife profile root and writes only at the caller-selected profile pointer.
    /// Conflict state is deliberately not accepted as a carrier.
    /// </summary>
    internal static JsonObject CreateAfterlifeCarrier(
        JsonObject afterlifeProfilesRoot,
        string selectedProfilePointer,
        params JsonObject[] activeWounds) =>
        AddActiveWoundsAtPointer(afterlifeProfilesRoot, selectedProfilePointer, activeWounds);

    internal static JsonObject CreateIdentityIndex(params JsonObject[] entries) => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = CloneArray(entries)
    };

    internal static JsonObject CreateIdentityEntry(
        string woundId = "wound_test_torn_side",
        string realm = "mortal_world",
        string ownerKind = "player",
        string ownerId = "player_current",
        string carrierPath = "game_state/player/wounds.json",
        string domain = "physical",
        string status = "active",
        int createdAtTurn = 42,
        string createdEventRef = "turn_42:wound_opened",
        int lastTransitionOrdinal = 1,
        string? terminalTransitionId = null,
        string semanticFingerprint = "sha256:0000000000000000000000000000000000000000000000000000000000000000") => new()
    {
        ["woundId"] = woundId,
        ["realm"] = realm,
        ["ownerKind"] = ownerKind,
        ["ownerId"] = ownerId,
        ["carrierPath"] = carrierPath,
        ["domain"] = domain,
        ["status"] = status,
        ["createdAtTurn"] = createdAtTurn,
        ["createdEventRef"] = createdEventRef,
        ["lastTransitionOrdinal"] = lastTransitionOrdinal,
        ["terminalTransitionId"] = terminalTransitionId,
        ["semanticFingerprint"] = semanticFingerprint
    };

    internal static JsonObject CreateHistory(params JsonObject[] transitions) => new()
    {
        ["schemaVersion"] = 1,
        ["nextOrdinal"] = transitions.Length + 1,
        ["transitions"] = CloneArray(transitions)
    };

    internal static JsonObject CreateTransition(
        string kind = "create",
        int ordinal = 1,
        int woundTransitionOrdinal = 1,
        bool terminal = false) => new()
    {
        ["transitionId"] = "wound_transition_test_001",
        ["woundId"] = "wound_test_torn_side",
        ["ordinal"] = ordinal,
        ["woundTransitionOrdinal"] = woundTransitionOrdinal,
        ["kind"] = kind,
        ["turn"] = 42,
        ["eventRef"] = "turn_42:wound_opened",
        ["operationKey"] = "operation_test_wound_001",
        ["beforeFingerprint"] = null,
        ["afterFingerprint"] = "wound-fingerprint-test-001",
        ["sourceFingerprint"] = "source-fingerprint-test-001",
        ["attemptId"] = "attempt_test_001",
        ["courseId"] = null,
        ["courseMilestoneOrdinal"] = null,
        ["cycleKey"] = null,
        ["paymentFingerprint"] = null,
        ["outputFingerprint"] = "sha256:" + new string('f', 64),
        ["readableSummary"] = "Рана зафиксирована после подтверждённого события.",
        ["terminal"] = terminal
    };

    internal static JsonObject CreateCommandsRoot(
        string sessionId = "session_wound_test",
        string requestId = "request_wound_test",
        string snapshotToken = "snapshot_wound_test",
        params JsonObject[] commands) => new()
    {
        ["schemaVersion"] = 1,
        ["sessionId"] = sessionId,
        ["requestId"] = requestId,
        ["snapshotToken"] = snapshotToken,
        ["commands"] = CloneArray(commands)
    };

    internal static JsonObject CreateCommand(string kind = "materialize") => new()
    {
        ["commandRef"] = "wound-command-test-001",
        ["kind"] = kind,
        ["targetBinding"] = new JsonObject
        {
            ["realm"] = "mortal_world",
            ["ownerKind"] = "player",
            ["ownerId"] = "player_current",
            ["carrierPath"] = "game_state/player/player.json#/activeWounds"
        },
        ["woundId"] = "wound_test_torn_side",
        ["routeId"] = "clean_and_suture",
        ["providerBinding"] = new JsonObject { ["providerKind"] = "self" },
        ["quotedCompensation"] = new JsonObject { ["kind"] = "none" },
        ["authorityFingerprint"] = "authority-fingerprint-test-001"
    };

    internal static JsonObject CreatePendingRoot(
        string sessionId = "session_wound_test",
        string requestId = "request_wound_test",
        string snapshotToken = "snapshot_wound_test",
        params JsonObject[] candidates) => new()
    {
        ["schemaVersion"] = 1,
        ["sessionId"] = sessionId,
        ["requestId"] = requestId,
        ["snapshotToken"] = snapshotToken,
        ["candidates"] = CloneArray(candidates)
    };

    internal static JsonObject CreatePendingCandidate(string kind = "construct_wound") => new()
    {
        ["candidateRef"] = "candidate_wound_test_001",
        ["kind"] = kind,
        ["safeContext"] = new JsonObject
        {
            ["summary"] = "Требуется безопасное уточнение описания ранения.",
            ["realm"] = "mortal_world"
        },
        ["allowedDecisionKinds"] = new JsonArray("none", "materialize"),
        ["severityBounds"] = new JsonObject { ["minimum"] = "I", ["maximum"] = "IV" },
        ["offendingIssues"] = new JsonArray(new JsonObject
        {
            ["path"] = "/classification/location",
            ["expected"] = "explicit physical location profile",
            ["expectedBounds"] = new JsonObject { ["minimumSeverity"] = "I", ["maximumSeverity"] = "IV" }
        }),
        ["semanticFingerprint"] = "candidate-fingerprint-test-001",
        ["receiptIdentity"] = new JsonObject { ["receiptRef"] = "receipt_wound_test_001" },
        ["transcriptPrefix"] = "Проверка ранения: ",
        ["preservedProposal"] = new JsonObject
        {
            ["decision"] = "materialize",
            ["readableSummary"] = "Сохранённая безопасная часть предложения."
        }
    };

    internal static JsonObject CreateActiveWound(
        string woundId = "wound_test_torn_side",
        string realm = "mortal_world",
        string ownerKind = "player",
        string ownerId = "player_current",
        string carrierPath = "game_state/player/wounds.json",
        string lifecycle = "active",
        string domain = "physical")
    {
        var wound = new JsonObject
        {
        ["schemaVersion"] = 1,
        ["woundId"] = woundId,
        ["lifecycle"] = lifecycle,
        ["owner"] = CreateOwner(realm, ownerKind, ownerId, carrierPath),
        ["origin"] = new JsonObject
        {
            ["eventRef"] = "turn_42:wound_opened",
            ["sourceKind"] = "combat_action",
            ["sourceId"] = "combat_action_test_001",
            ["sourceState"] = "active",
            ["createdAtTurn"] = 42,
            ["createdAtCycleId"] = null,
            ["opportunityId"] = "opportunity_test_001",
            ["guaranteedTriggerId"] = null,
            ["readableCause"] = "Рваная рана получена в подтверждённом столкновении."
        },
        ["classification"] = new JsonObject
        {
            ["domain"] = domain,
            ["woundType"] = "Рваная режущая травма",
            ["locationProfile"] = new JsonObject
            {
                ["kind"] = "anatomical",
                ["readableLocus"] = "левый бок",
                ["authorityKind"] = "body_part",
                ["authorityRef"] = "left_side",
                ["affectedSide"] = "left"
            }
        },
        ["display"] = new JsonObject
        {
            ["name"] = "Рваная рана левого бока",
            ["description"] = "Края раны расходятся при резком движении.",
            ["visibleSymptoms"] = new JsonArray("кровотечение", "боль при движении"),
            ["prognosis"] = "При своевременной обработке рана заживёт без осложнений.",
            ["visibility"] = "known_to_player",
            ["acquisitionNarration"] = "Острый край распорол бок в короткой схватке."
        },
        ["severity"] = new JsonObject
        {
            ["value"] = "II",
            ["rank"] = 2,
            ["maximumAtCreation"] = "II",
            ["lastChangeEventRef"] = "turn_42:wound_opened"
        },
        ["care"] = new JsonObject
        {
            ["state"] = "untreated",
            ["stabilizedAtTurn"] = null,
            ["activeCourseId"] = null,
            ["lastAttemptId"] = null
        },
        ["complications"] = new JsonArray(),
        ["consequences"] = new JsonObject
        {
            ["slotBudget"] = 2,
            ["slotsUsed"] = 2,
            ["ownedEffectSources"] = CreateOwnedEffectSourcesForTarget(
                woundId,
                realm,
                ResolveEffectTargetKind(ownerKind),
                ("effect_wound_test_bleeding", "definition_wound_test_bleeding", "periodic_damage"),
                ("effect_wound_test_pain", "definition_wound_test_pain", "action_control")),
            ["entries"] = new JsonArray(
                new JsonObject
                {
                    ["slot"] = 1,
                    ["profileKey"] = "periodic_damage",
                    ["effectId"] = "effect_wound_test_bleeding",
                    ["readableSummary"] = "Рана продолжает кровоточить."
                },
                new JsonObject
                {
                    ["slot"] = 2,
                    ["profileKey"] = "action_control",
                    ["effectId"] = "effect_wound_test_pain",
                    ["readableSummary"] = "Резкие движения затруднены."
                })
        },
        ["treatment"] = new JsonObject
        {
            ["diagnosisPaths"] = new JsonArray(),
            ["routes"] = domain == "spiritual"
                ? new JsonArray()
                : new JsonArray(CreateMortalProcedureRoute()),
            ["knownRouteIds"] = domain == "spiritual"
                ? new JsonArray()
                : new JsonArray("clean_and_suture"),
            ["completedRouteIds"] = new JsonArray()
        },
        ["recovery"] = new JsonObject
        {
            ["mode"] = domain == "spiritual" ? "progressive" : "requires_stabilization",
            ["clockKind"] = domain == "spiritual" ? "afterlife_safe_cycle" : "mortal_world_time",
            ["cadence"] = domain == "spiritual" ? 1 : 86400,
            ["currentStepProgress"] = 0,
            ["currentStepThreshold"] = domain == "spiritual" ? 4 : 3,
            ["lastTickKey"] = null,
            ["blockers"] = domain == "spiritual"
                ? new JsonArray()
                : new JsonArray("not_stabilized"),
            ["carryOverflow"] = true,
            ["deteriorationPolicy"] = null
        },
        ["relations"] = new JsonObject
        {
            ["priorWoundId"] = null,
            ["legacyRefs"] = new JsonArray(),
            ["independentEffectRefs"] = new JsonArray()
        },
        ["lastTransition"] = new JsonObject
        {
            ["transitionId"] = "wound_transition_test_001",
            ["ordinal"] = 1,
            ["turn"] = 42,
            ["kind"] = "create"
        }
        };

        if (string.Equals(domain, "spiritual", StringComparison.Ordinal))
            ConfigureSpiritualDefaults(wound, woundId, realm, ownerKind);
        return wound;
    }

    internal static JsonObject CreateSpiritualActiveWound(
        string woundId = "wound_spiritual_test",
        string realm = "chaos_sea",
        string ownerKind = "player_soul",
        string ownerId = "player_soul_current",
        string carrierPath = "game_state/meta/afterlife_entity_profiles.json#/playerSoul") =>
        CreateActiveWound(
            woundId,
            realm,
            ownerKind,
            ownerId,
            carrierPath,
            domain: "spiritual");

    private static void ConfigureSpiritualDefaults(
        JsonObject wound,
        string woundId,
        string realm,
        string ownerKind)
    {
        wound["classification"] = new JsonObject
        {
            ["domain"] = "spiritual",
            ["woundType"] = "Трещина духовной целостности",
            ["locationProfile"] = new JsonObject
            {
                ["kind"] = "spiritual_axis",
                ["readableLocus"] = "воля и духовное равновесие",
                ["authorityKind"] = "spiritual_axis",
                ["authorityRef"] = "will_and_balance",
                ["affectedSide"] = "self"
            }
        };
        wound["display"]!["name"] = "Трещина духовной целостности";
        wound["display"]!["description"] =
            "Пережитое столкновение нарушило волю и духовное равновесие.";
        wound["display"]!["visibleSymptoms"] = new JsonArray(
            "тяжесть духовных действий",
            "неуверенность в противостоянии");
        wound["display"]!["prognosis"] =
            "Рана поддаётся духовному исцелению и естественному восстановлению.";
        wound["display"]!["acquisitionNarration"] =
            "Чужое давление оставило трещину в духовной целостности.";

        var sources = CreateOwnedEffectSourcesForTarget(
            woundId,
            realm,
            ResolveEffectTargetKind(ownerKind),
            ("effect_spiritual_roll", "definition_spiritual_roll", "spiritual_roll_hindrance"),
            ("effect_spiritual_cost", "definition_spiritual_cost", "spiritual_action_cost_burden"));
        sources["definitions"]![0]!["components"]![0]!["componentId"] =
            "component_spiritual_roll";
        sources["definitions"]![1]!["components"]![0]!["componentId"] =
            "component_spiritual_cost";
        sources["definitions"]![1]!["components"]![0]!["payload"]!["magnitude"] = 1;
        sources["definitions"]![0]!["triggers"]![0]!["componentIds"] =
            new JsonArray("component_spiritual_roll");
        sources["definitions"]![1]!["triggers"]![0]!["componentIds"] =
            new JsonArray("component_spiritual_cost");
        wound["consequences"]!["ownedEffectSources"] = sources;
        wound["consequences"]!["entries"] = new JsonArray(
            new JsonObject
            {
                ["slot"] = 1,
                ["profileKey"] = "spiritual_roll_hindrance",
                ["effectId"] = "effect_spiritual_roll",
                ["readableSummary"] = "Духовные проверки проходят с помехой."
            },
            new JsonObject
            {
                ["slot"] = 2,
                ["profileKey"] = "spiritual_action_cost_burden",
                ["effectId"] = "effect_spiritual_cost",
                ["readableSummary"] = "Духовные действия требуют дополнительного усилия."
            });
    }

    internal static JsonObject CreateOwnedEffectSources(
        string woundId,
        string realm,
        params (string EffectId, string DefinitionKey, string Profile)[] roots) =>
        CreateOwnedEffectSourcesForTarget(woundId, realm, "player", roots);

    internal static JsonObject CreateOwnedEffectSourcesForTarget(
        string woundId,
        string realm,
        string targetKind,
        params (string EffectId, string DefinitionKey, string Profile)[] roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var definitions = new JsonArray();
        var rootBindings = new JsonArray();
        foreach (var (effectId, definitionKey, profile) in roots)
        {
            definitions.Add(CreateOwnedEffectDefinition(
                woundId,
                realm,
                definitionKey,
                profile,
                $"stack_{definitionKey}",
                targetKind));
            rootBindings.Add(CreateRootBinding(effectId, definitionKey));
        }

        return new JsonObject
        {
            ["definitions"] = definitions,
            ["rootBindings"] = rootBindings
        };
    }

    internal static JsonObject CreateOwnedEffectDefinition(
        string woundId,
        string realm,
        string definitionKey,
        string profile = "periodic_damage",
        string? stackKey = null,
        string targetKind = "player")
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(profile);
        definition["definitionKey"] = definitionKey;
        definition["allowedRealms"] = new JsonArray(realm);
        definition["allowedTargetKinds"] = new JsonArray(targetKind);
        definition["parameterBounds"] = new JsonObject();
        definition["stacking"] = new JsonObject
        {
            ["stackKey"] = stackKey ?? $"stack_{definitionKey}",
            ["policy"] = "independent",
            ["maxStacks"] = 1,
            ["atMaximum"] = "no_change",
            ["refreshMode"] = null,
            ["mergeRule"] = null
        };
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "active",
            ["onSourceLoss"] = "expire"
        };
        definition["links"] = new JsonArray(new JsonObject
        {
            ["kind"] = "wound",
            ["targetId"] = woundId,
            ["role"] = "source"
        });
        if (string.Equals(profile, "wound_consequence", StringComparison.Ordinal))
            definition["components"]![0]!["payload"]!["woundId"] = woundId;
        return definition;
    }

    private static string ResolveEffectTargetKind(string ownerKind) => ownerKind switch
    {
        "player" or "player_soul" => "player",
        "npc" => "npc",
        "combatant" or "combatant_member" => "combatant",
        "guardian" => "guardian",
        "resident" => "resident",
        "radiant_actor" => "radiant_actor",
        "afterlife_actor" => "afterlife_actor",
        _ => throw new ArgumentOutOfRangeException(
            nameof(ownerKind),
            ownerKind,
            "Unsupported wound owner kind for effect-target test authority.")
    };

    internal static JsonObject CreateRootBinding(
        string effectId,
        string definitionKey) => new()
    {
        ["effectId"] = effectId,
        ["definitionKey"] = definitionKey
    };

    internal static JsonObject CreateApplyDefinitionRoot(
        string woundId,
        string realm,
        string definitionKey,
        string leafDefinitionKey,
        int maxExpansion = 2)
    {
        var definition = CreateOwnedEffectDefinition(
            woundId,
            realm,
            definitionKey,
            "event_reaction");
        var payload = definition["components"]![0]!["payload"]!.AsObject();
        payload["resultKind"] = "apply_definition";
        payload["dependency"] = "before_current_event";
        payload["definitionKey"] = leafDefinitionKey;
        payload["parameters"] = new JsonObject();
        payload["maxExpansion"] = maxExpansion;
        return definition;
    }

    internal static JsonArray Repeat(int count, Func<int, JsonObject> factory)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(factory);
        var result = new JsonArray();
        for (var index = 0; index < count; index++) result.Add(factory(index).DeepClone());
        return result;
    }

    internal static string DistinctMortalProfile(int index) =>
        DistinctMortalProfiles[index % DistinctMortalProfiles.Length];

    private static JsonObject CreatePlayerCarrierOwner() => new()
    {
        ["realm"] = "mortal_world", ["ownerKind"] = "player", ["ownerId"] = "player_current"
    };

    private static JsonObject CreateOwner(
        string realm,
        string ownerKind,
        string ownerId,
        string carrierPath) => new()
    {
        ["realm"] = realm,
        ["ownerKind"] = ownerKind,
        ["ownerId"] = ownerId,
        ["carrierPath"] = carrierPath
    };

    private static JsonObject CreateMortalProcedureRoute() => new()
    {
        ["routeId"] = "clean_and_suture",
        ["displayName"] = "Очистить и наложить швы",
        ["visibility"] = "known_to_player",
        ["mode"] = "procedure",
        ["requirements"] = new JsonArray(
            new JsonObject
            {
                ["kind"] = "item_quantity",
                ["itemRef"] = "sterile_thread",
                ["quantity"] = 1,
                ["ownerRole"] = "provider"
            },
            new JsonObject
            {
                ["kind"] = "skill_tier",
                ["capabilityRef"] = "field_medicine",
                ["minimumTier"] = 2,
                ["actorRole"] = "provider"
            }),
        ["resourcePolicy"] = new JsonObject
        {
            ["reserveBeforeResolution"] = true,
            ["consumeOn"] = new JsonArray("success", "partial_success", "failed_attempt"),
            ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"),
            ["mutations"] = new JsonArray(new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "common",
                ["milestoneOrdinal"] = null,
                ["requirementIndex"] = 0
            })
        },
        ["resolution"] = new JsonObject
        {
            ["formulaKey"] = "mortal_wound_procedure_v1",
            ["difficulty"] = 15,
            ["rollSource"] = "accepted_d20",
            ["criticalPolicy"] = "natural_20_first_natural_1_last",
            ["modifierSource"] = new JsonObject
            {
                ["kind"] = "resolved_skill_tier",
                ["requirementIndex"] = 1
            }
        },
        ["outcomes"] = new JsonArray(
            new JsonObject
            {
                ["bandId"] = "clean_success",
                ["minimumMargin"] = 5,
                ["maximumMargin"] = null,
                ["category"] = "success",
                ["result"] = new JsonArray(
                    new JsonObject { ["kind"] = "stabilize" },
                    new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 })
            },
            new JsonObject
            {
                ["bandId"] = "clean_partial",
                ["minimumMargin"] = 0,
                ["maximumMargin"] = 4,
                ["category"] = "partial_success",
                ["result"] = new JsonArray(
                    new JsonObject { ["kind"] = "stabilize" },
                    new JsonObject { ["kind"] = "add_recovery", ["points"] = 1 })
            },
            new JsonObject
            {
                ["bandId"] = "clean_no_improvement",
                ["minimumMargin"] = -4,
                ["maximumMargin"] = -1,
                ["category"] = "failed_attempt",
                ["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" })
            },
            new JsonObject
            {
                ["bandId"] = "clean_complication",
                ["minimumMargin"] = null,
                ["maximumMargin"] = -5,
                ["category"] = "failed_attempt",
                ["result"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "add_complication",
                    ["complicationDraft"] = new JsonObject
                    {
                        ["complications"] = new JsonArray(new JsonObject
                        {
                            ["complicationRef"] = "irritation",
                            ["kind"] = "pain",
                            ["state"] = "active",
                            ["displayName"] = "Раздражённые края раны",
                            ["treatmentDifficultyModifier"] = 1,
                            ["visibility"] = "known_to_player"
                        }),
                        ["consequenceDefinitions"] = new JsonArray()
                    }
                })
            }),
        ["interruption"] = null
    };

    private static JsonObject AddActiveWoundsAtPointer(JsonObject root, string pointer, JsonObject[] activeWounds)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(pointer);
        var clone = root.DeepClone().AsObject();
        var target = ResolveObjectPointer(clone, pointer);
        target["activeWounds"] = CloneArray(activeWounds);
        return clone;
    }

    private static JsonObject ResolveObjectPointer(JsonObject root, string pointer)
    {
        if (!pointer.StartsWith("/", StringComparison.Ordinal))
            throw new ArgumentException("A selected carrier must use an absolute JSON pointer.", nameof(pointer));

        JsonNode current = root;
        foreach (var token in pointer[1..].Split('/', StringSplitOptions.None))
        {
            var segment = token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            current = current switch
            {
                JsonObject obj when obj[segment] is not null => obj[segment]!,
                JsonArray array when int.TryParse(segment, out var index) && index >= 0 && index < array.Count && array[index] is not null => array[index]!,
                _ => throw new ArgumentException($"JSON pointer '{pointer}' does not resolve to an existing node.", nameof(pointer))
            };
        }

        return current as JsonObject
            ?? throw new ArgumentException("A selected carrier pointer must resolve to an object.", nameof(pointer));
    }

    private static JsonArray CloneArray(IEnumerable<JsonObject> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var clone = new JsonArray();
        foreach (var node in nodes)
        {
            ArgumentNullException.ThrowIfNull(node);
            clone.Add(node.DeepClone());
        }

        return clone;
    }
}
