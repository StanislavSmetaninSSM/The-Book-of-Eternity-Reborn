using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

/// <summary>Strict, deterministic version-1 JSON data for future wound contract tests.</summary>
internal static class WoundContractTestData
{
    internal const int ActiveWoundLimit = 2_000;
    internal const int HistoryRowLimit = 20_000;
    internal const int CommandLimit = 128;
    internal const int PendingCandidateLimit = 64;
    internal const int TreatmentPathLimit = 32;
    internal const int DiagnosisPathLimit = 32;
    internal const int RequirementLimit = 16;
    internal const int ComplicationLimit = 16;
    internal const int ConsequenceLimit = 4;
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
        string carrierPath = "game_state/player/player.json#/activeWounds",
        string domain = "physical",
        string status = "active") => new()
    {
        ["woundId"] = woundId,
        ["realm"] = realm,
        ["ownerKind"] = ownerKind,
        ["ownerId"] = ownerId,
        ["carrierPath"] = carrierPath,
        ["domain"] = domain,
        ["status"] = status,
        ["createdAtTurn"] = 42,
        ["createdEventRef"] = "turn_42:wound_opened",
        ["lastTransitionOrdinal"] = 1,
        ["terminalTransitionId"] = null,
        ["semanticFingerprint"] = "wound-fingerprint-test-001"
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

    internal static JsonObject CreateActiveWound() => new()
    {
        ["schemaVersion"] = 1,
        ["woundId"] = "wound_test_torn_side",
        ["lifecycle"] = "active",
        ["owner"] = CreateOwner(),
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
            ["domain"] = "physical",
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
            ["routes"] = new JsonArray(CreateMortalProcedureRoute()),
            ["knownRouteIds"] = new JsonArray("clean_and_suture"),
            ["completedRouteIds"] = new JsonArray()
        },
        ["recovery"] = new JsonObject
        {
            ["mode"] = "requires_stabilization",
            ["clockKind"] = "mortal_world_time",
            ["cadence"] = 86400,
            ["currentStepProgress"] = 0,
            ["currentStepThreshold"] = 3,
            ["lastTickKey"] = null,
            ["blockers"] = new JsonArray("not_stabilized"),
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

    internal static JsonArray Repeat(int count, Func<int, JsonObject> factory)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(factory);
        var result = new JsonArray();
        for (var index = 0; index < count; index++) result.Add(factory(index).DeepClone());
        return result;
    }

    private static JsonObject CreatePlayerCarrierOwner() => new()
    {
        ["realm"] = "mortal_world", ["ownerKind"] = "player", ["ownerId"] = "player_current"
    };

    private static JsonObject CreateOwner() => new()
    {
        ["realm"] = "mortal_world", ["ownerKind"] = "player", ["ownerId"] = "player_current",
        ["carrierPath"] = "game_state/player/player.json#/activeWounds"
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
                ["itemId"] = "sterile_thread",
                ["quantity"] = 1
            },
            new JsonObject
            {
                ["kind"] = "skill_tier",
                ["skillId"] = "field_medicine",
                ["minimumTier"] = 2
            }),
        ["resourcePolicy"] = new JsonObject
        {
            ["reserveBeforeResolution"] = true,
            ["consumeOn"] = new JsonArray("success", "partial_success", "failed_attempt"),
            ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"),
            ["mutations"] = new JsonArray()
        },
        ["resolution"] = new JsonObject
        {
            ["formulaKey"] = "mortal_wound_procedure_v1",
            ["difficulty"] = 15,
            ["rollSource"] = "accepted_d20"
        },
        ["outcomes"] = new JsonArray(
            new JsonObject
            {
                ["minimumMargin"] = 5,
                ["results"] = new JsonArray("stabilize", "reduce_one")
            },
            new JsonObject
            {
                ["minimumMargin"] = 0,
                ["results"] = new JsonArray("stabilize", "add_recovery:1")
            },
            new JsonObject
            {
                ["minimumMargin"] = -4,
                ["results"] = new JsonArray("no_improvement")
            },
            new JsonObject
            {
                ["maximumMargin"] = -5,
                ["results"] = new JsonArray("add_complication:irritation")
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
