using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// RED boundary for T061.  These rows deliberately own no proof, requirement,
/// reservation, dice, Fate, receipt, or fingerprint construction.  A future
/// fixture must obtain accepted state through the canonical registry/lease path;
/// the reflection adapter below then drives only the public-to-assembly planner
/// factories and mode resolver entries documented by the treatment contract.
/// </summary>
public sealed partial class MortalWoundTreatmentResolverTests
{
    private const string PlannerTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentPlanner";

    public static IEnumerable<object[]> ProcedureRows => Rows(
        "procedure_normal_uses_lowest_free_die",
        "procedure_advantage_uses_two_contiguous_dice",
        "procedure_disadvantage_uses_two_contiguous_dice",
        "procedure_player_natural_one_reserves_oldest_fate_shield");

    public static IEnumerable<object[]> CourseRows => Rows(
        "course_first_milestone_is_ready_at_inclusive_due_time",
        "course_unsatisfied_dose_requirement_has_no_reservation");

    public static IEnumerable<object[]> GuaranteedRows => Rows(
        "guaranteed_current_capability_proof_stabilizes",
        "guaranteed_severity_one_heal_has_empty_legacy_array");

    [Fact]
    public void IndependentControl_ExistingWoundAndEmptyHistoryRemainParseable()
    {
        var wound = WoundMaterializationContract.Parse(
            WoundContractTestData.CreateActiveWound().ToJsonString(),
            "wound");
        var history = WoundHistoryState.Parse(
            WoundContractTestData.CreateHistory().ToJsonString(),
            "history");

        Assert.True(wound.IsValid, DescribeIssues(wound.Issues));
        Assert.NotNull(wound.Wound);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.NotNull(history.State);

    }

    [Theory]
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public void FixtureControl_StrictModeSpecificRouteAndEmptyHistoryParse(string mode)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        var route = mode switch
        {
            "procedure" => StrictProcedureRoute(),
            "course" => StrictCourseRoute(),
            "guaranteed" => StrictGuaranteedRoute(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        wound["treatment"]!["routes"] = new JsonArray(route);
        wound["treatment"]!["knownRouteIds"] = new JsonArray(route["routeId"]!.DeepClone());
        var parsed = WoundMaterializationContract.Parse(wound.ToJsonString(), "wound");
        var history = WoundHistoryState.Parse(WoundContractTestData.CreateHistory().ToJsonString(), "history");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.Equal(mode, Assert.Single(parsed.Wound!.Treatment.Routes).Mode);
    }

    [Theory]
    [MemberData(nameof(SemanticRows))]
    public void FixtureControl_EachRetainedScenarioHasAValidAndDistinctSemanticInput(
        string scenario,
        string mode)
    {
        var descriptor = CreateScenario(scenario, mode);
        var wound = WoundMaterializationContract.Parse(descriptor.Before.ToJsonString(), "wound");
        var history = WoundHistoryState.Parse(descriptor.History.ToJsonString(), "history");

        Assert.True(wound.IsValid, DescribeIssues(wound.Issues));
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.Equal(mode, Assert.Single(wound.Wound!.Treatment.Routes).Mode);
        Assert.Equal(descriptor.RouteId, descriptor.Before["treatment"]!["knownRouteIds"]![0]!.GetValue<string>());
        Assert.Equal(descriptor.RollMode, descriptor.AcceptedState["rollMode"]!.GetValue<string>());
        Assert.Equal(descriptor.WorldMinute, descriptor.AcceptedState["worldMinute"]!.GetValue<long>());
        Assert.Equal(descriptor.RequirementsAvailable, descriptor.AcceptedState["requirementsAvailable"]!.GetValue<bool>());
        Assert.Equal(descriptor.ExpectedNaturalRoll,
            descriptor.AcceptedState["acceptedDice"]!.AsArray()[descriptor.ExpectedSelectedSourceIndex]!.GetValue<int>());
        if (scenario.Contains("severity_one", StringComparison.Ordinal))
        {
            Assert.Equal("I", descriptor.Before["severity"]!["value"]!.GetValue<string>());
            Assert.Empty(descriptor.Before["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"]![0]!["legacies"]!.AsArray());
        }
    }

    [Theory]
    [MemberData(nameof(CourseRows))]
    public void FixtureControl_CanonicalItemCarrierRootsExposeOnlyTheCurrentCourseDose(
        string scenario)
    {
        var descriptor = CreateScenario(scenario, "course");
        var sterileThread = CreateCanonicalStack("sterile_thread", 2);
        var dose = descriptor.RequirementsAvailable
            ? CreateCanonicalStack("antibiotic_dose", 1)
            : null;
        var catalog = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            PlayerInventoryRoot(dose),
            NpcCoreRoot(sterileThread),
            null,
            null,
            null,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal)));

        Assert.Empty(catalog.Issues);
        Assert.Single(catalog.ByItemId["sterile_thread"]);
        Assert.Equal(descriptor.RequirementsAvailable,
            catalog.ByItemId.ContainsKey("antibiotic_dose"));
    }

    [Fact]
    public void FixtureControl_CourseStartIsFirstMilestoneAtItsInclusiveDueTime()
    {
        var descriptor = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        var wound = WoundMaterializationContract.Parse(descriptor.Before.ToJsonString(), "wound");
        var history = WoundHistoryState.Parse(descriptor.History.ToJsonString(), "history");

        Assert.True(wound.IsValid, DescribeIssues(wound.Issues));
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.Null(wound.Wound!.Care.ActiveCourseId);
        Assert.Empty(history.State!.Transitions);
        Assert.Equal(1, history.State.NextOrdinal);
        var first = descriptor.Before["treatment"]!["routes"]![0]!["outcomes"]![0]!.AsObject();
        Assert.Equal(1, first["ordinal"]!.GetValue<int>());
        Assert.Equal(0L, first["afterMinutes"]!.GetValue<long>());
        Assert.Equal(0L, descriptor.WorldMinute);
    }

    [Fact]
    public void FixtureControl_PlayerAndProviderSkillsUseProductionValidActiveSkillShapes()
    {
        using var player = JsonDocument.Parse(
            CreateTreatmentSkill("skill_field_medicine_01", "field_medicine").ToJsonString());
        using var provider = JsonDocument.Parse(
            CreateTreatmentSkill("skill_guaranteed_care_01", "exact_materialized_healing_source").ToJsonString());

        Assert.True(ValidationService.IsProductionValidMortalActiveSkill(player.RootElement));
        Assert.True(ValidationService.IsProductionValidMortalActiveSkill(provider.RootElement));
    }

    [Theory]
    [MemberData(nameof(ProcedureRows))]
    public void FixtureControl_ProductionEffectRootsExposeOrderedFateAndRollMode(
        string scenario)
    {
        var descriptor = CreateScenario(scenario, "procedure");
        var roots = CreateEffectRoots(descriptor);
        var snapshot = EffectMechanicsSnapshot.Build(new EffectMechanicsInput(
            new EffectCarrierCatalogInput(roots.PlayerEffects, null, null, null, null, null),
            roots.IdentityIndex));

        Assert.True(snapshot.IsAccepted, DescribeIssues(snapshot.Issues));
        var carrierOrder = roots.PlayerEffects["activeEffects"]!.AsArray()
            .OfType<JsonObject>()
            .Where(effect => effect["effectId"]!.GetValue<string>()
                .StartsWith("effect_fate_shield_", StringComparison.Ordinal))
            .Select(effect => effect["effectId"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(
            new[] { "effect_fate_shield_newer", "effect_fate_shield_older" },
            carrierOrder);
        var effects = roots.PlayerEffects["activeEffects"]!.AsArray()
            .OfType<JsonObject>()
            .Where(effect => effect["effectId"]!.GetValue<string>()
                .StartsWith("effect_fate_shield_", StringComparison.Ordinal))
            .OrderBy(effect => effect["chronology"]!["createdAtTurn"]!.GetValue<int>())
            .ToArray();
        Assert.Collection(
            effects,
            effect =>
            {
                Assert.Equal("effect_fate_shield_older", effect["effectId"]!.GetValue<string>());
                Assert.Equal(37, effect["chronology"]!["createdAtTurn"]!.GetValue<int>());
            },
            effect =>
            {
                Assert.Equal("effect_fate_shield_newer", effect["effectId"]!.GetValue<string>());
                Assert.Equal(42, effect["chronology"]!["createdAtTurn"]!.GetValue<int>());
            });
        var identities = roots.IdentityIndex["entries"]!.AsArray().OfType<JsonObject>()
            .ToDictionary(entry => entry["effectId"]!.GetValue<string>(), StringComparer.Ordinal);
        foreach (var effect in effects)
        {
            var effectId = effect["effectId"]!.GetValue<string>();
            var createdAtTurn = effect["chronology"]!["createdAtTurn"]!.GetValue<int>();
            var identity = identities[effectId];
            var transition = identity["transitions"]![0]!.AsObject();
            Assert.Equal(createdAtTurn, identity["createdAtTurn"]!.GetValue<int>());
            Assert.Equal(createdAtTurn, transition["turn"]!.GetValue<int>());
            Assert.Equal(effect["chronology"]!["createdEventRef"]!.GetValue<string>(),
                transition["eventRef"]!.GetValue<string>());
            Assert.Equal(effect["chronology"]!["lastTransitionId"]!.GetValue<string>(),
                transition["transitionId"]!.GetValue<string>());
        }
    }

    [Theory]
    [MemberData(nameof(ProcedureRows))]
    public void PrepareProcedureRequest_ResolvesOnlyThroughLeaseBoundAcceptedState(
        string scenario) =>
        ExecutePreparedFlow(CreateScenario(scenario, "procedure"));

    [Theory]
    [MemberData(nameof(CourseRows))]
    public void PrepareCourseMilestoneRequest_ResolvesOnlyThroughLeaseBoundAcceptedState(
        string scenario) =>
        ExecutePreparedFlow(CreateScenario(scenario, "course"));

    [Theory]
    [MemberData(nameof(GuaranteedRows))]
    public void PrepareGuaranteedRequest_ResolvesOnlyThroughLeaseBoundAcceptedState(
        string scenario) =>
        ExecutePreparedFlow(CreateScenario(scenario, "guaranteed"));

    private static IEnumerable<object[]> Rows(params string[] values) =>
        values.Select(static value => new object[] { value });

    public static IEnumerable<object[]> SemanticRows =>
        ProcedureRows.Select(row => new object[] { (string)row[0], "procedure" })
            .Concat(CourseRows.Select(row => new object[] { (string)row[0], "course" }))
            .Concat(GuaranteedRows.Select(row => new object[] { (string)row[0], "guaranteed" }));

    private static ResolverScenario CreateScenario(string name, string mode)
    {
        var before = WoundContractTestData.CreateActiveWound();
        var route = before["treatment"]!["routes"]![0]!.DeepClone().AsObject();
        ConfigureModeRoute(route, mode, name);
        before["treatment"]!["routes"] = new JsonArray(route);
        before["treatment"]!["knownRouteIds"] = new JsonArray(route["routeId"]!.DeepClone());
        var acceptedState = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["sessionId"] = "session_t061",
            ["requestId"] = "request_t061",
            ["snapshotToken"] = "snapshot_t061",
            ["turn"] = 42,
            ["providerKind"] = "npc",
            ["providerId"] = "field_medic_01",
            ["targetKind"] = "player",
            ["targetId"] = "player_current",
            ["locationId"] = "loc_field_clinic_001",
            ["worldMinute"] = 1_260L,
            ["woundId"] = "wound_test_torn_side",
            ["eventRef"] = "turn_42:treatment_event_" + name,
            ["acceptedDice"] = new JsonArray(4, 17, 1, 20),
            ["skillRows"] = new JsonArray("skill_field_medicine_01"),
            ["canonicalReads"] = new JsonArray(
                "wound_carriers",
                "wound_identity_index",
                "wound_history",
                "world_time",
                "turn_request",
                "effect_carriers",
                "effect_identity_index",
                "skills")
        };

        // Each row has a distinct production input coordinate.  These are fixture
        // sources only; the planner must read/validate their canonical equivalents
        // itself and must not accept this object as authority.
        acceptedState["operationLabel"] = name;
        acceptedState["requestedMode"] = mode;
        var semantic = name switch
        {
            "procedure_normal_uses_lowest_free_die" => new ScenarioSemantics("normal", new[] { 0 }, 0, 17, 1_260, true, "Resolved", "success", 2, null),
            "procedure_advantage_uses_two_contiguous_dice" => new ScenarioSemantics("advantage", new[] { 0, 1 }, 1, 19, 1_260, true, "Resolved", "success", 2, null),
            "procedure_disadvantage_uses_two_contiguous_dice" => new ScenarioSemantics("disadvantage", new[] { 0, 1 }, 0, 4, 1_260, true, "Resolved", "failed_attempt", 1, null),
            "procedure_player_natural_one_reserves_oldest_fate_shield" => new ScenarioSemantics("normal", new[] { 0 }, 0, 1, 1_260, true, "Resolved", "failed_attempt", 1, "effect_fate_shield_older"),
            "course_first_milestone_is_ready_at_inclusive_due_time" => new ScenarioSemantics("normal", new[] { 0 }, 0, 17, 0, true, "Resolved", "success", 0, null),
            "course_unsatisfied_dose_requirement_has_no_reservation" => new ScenarioSemantics("normal", new[] { 0 }, 0, 17, 0, false, "PreparationRejected", null, 0, null),
            "guaranteed_current_capability_proof_stabilizes" => new ScenarioSemantics("normal", new[] { 0 }, 0, 17, 1_260, true, "Resolved", "success", 1, null),
            "guaranteed_severity_one_heal_has_empty_legacy_array" => new ScenarioSemantics("normal", new[] { 0 }, 0, 17, 1_260, true, "Resolved", "success", 1, null),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Retained scenarios must have concrete semantics.")
        };
        acceptedState["rollMode"] = semantic.RollMode;
        acceptedState["acceptedDice"] = semantic.RollMode switch
        {
            "normal" when name.Contains("natural_one", StringComparison.Ordinal) => new JsonArray(1, 17),
            "normal" => new JsonArray(17, 4, 1, 20),
            _ => new JsonArray(4, 19, 7)
        };
        acceptedState["worldMinute"] = semantic.WorldMinute;
        acceptedState["requirementsAvailable"] = semantic.RequirementsAvailable;
        if (name == "guaranteed_severity_one_heal_has_empty_legacy_array")
        {
            before["severity"]!["value"] = "I";
            before["severity"]!["rank"] = 1;
            before["severity"]!["maximumAtCreation"] = "I";
            before["consequences"]!["slotBudget"] = 1;
            before["consequences"]!["slotsUsed"] = 1;
            before["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray().RemoveAt(1);
            before["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray().RemoveAt(1);
            before["consequences"]!["entries"]!.AsArray().RemoveAt(1);
            route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
            {
                ["kind"] = "heal",
                ["legacies"] = new JsonArray()
            });
        }

        return new ResolverScenario(
            name,
            mode,
            acceptedState,
            CreateHistoryFor(name, mode),
            before,
            "operation_t061_" + name,
            route["routeId"]!.GetValue<string>(),
            "turn_42:treatment_event_" + name,
            semantic.ExpectedBoundary,
            semantic.ExpectedCategory,
            semantic.RollMode,
            semantic.SourceIndices,
            semantic.SelectedSourceIndex,
            semantic.NaturalRoll,
            semantic.WorldMinute,
            semantic.RequirementsAvailable,
            semantic.ExpectedIntentCount,
            semantic.ExpectedFateEffectId);
    }

    private static void ConfigureModeRoute(JsonObject route, string mode, string name)
    {
        var strict = mode switch
        {
            "procedure" => StrictProcedureRoute(),
            "course" => StrictCourseRoute(),
            "guaranteed" => StrictGuaranteedRoute(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        route.Clear();
        foreach (var pair in strict)
            route[pair.Key] = pair.Value?.DeepClone();
        route["routeId"] = mode + "_t061_" + name;
    }

    private static JsonObject StrictProcedureRoute() => new()
    {
        ["routeId"] = "procedure_v1", ["displayName"] = "Procedure", ["visibility"] = "known_to_player", ["mode"] = "procedure",
        ["requirements"] = new JsonArray(
            new JsonObject { ["kind"] = "item_quantity", ["itemRef"] = "sterile_thread", ["quantity"] = 1, ["ownerRole"] = "provider" },
            // The procedure is performed on the player target.  The provider still
            // supplies the sterile thread, while the target's accepted skill is the
            // only legal source of the player-owned d20/Fate reaction below.
            new JsonObject { ["kind"] = "skill_tier", ["capabilityRef"] = "field_medicine", ["minimumTier"] = 2, ["actorRole"] = "target" }),
        ["resourcePolicy"] = Policy(new JsonArray("success", "partial_success", "failed_attempt"), new JsonArray(new JsonObject { ["kind"] = "consume_requirement", ["scope"] = "common", ["milestoneOrdinal"] = null, ["requirementIndex"] = 0 })),
        ["resolution"] = new JsonObject { ["formulaKey"] = "mortal_wound_procedure_v1", ["difficulty"] = 15, ["rollSource"] = "accepted_d20", ["criticalPolicy"] = "natural_20_first_natural_1_last", ["modifierSource"] = new JsonObject { ["kind"] = "resolved_skill_tier", ["requirementIndex"] = 1 } },
        ["outcomes"] = new JsonArray(
            Band("success", 5, null, "success", new JsonArray(new JsonObject { ["kind"] = "stabilize" }, new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 })),
            Band("partial", 0, 4, "partial_success", new JsonArray(new JsonObject { ["kind"] = "stabilize" }, new JsonObject { ["kind"] = "add_recovery", ["points"] = 1 })),
            Band("failed", null, -1, "failed_attempt", new JsonArray(new JsonObject { ["kind"] = "no_improvement" }))),
        ["interruption"] = null
    };

    private static JsonObject StrictCourseRoute() => new()
    {
        ["routeId"] = "course_v1", ["displayName"] = "Course", ["visibility"] = "known_to_player", ["mode"] = "course",
        ["requirements"] = new JsonArray(new JsonObject { ["kind"] = "provider", ["providerRef"] = "field_medic_01" }),
        ["resourcePolicy"] = Policy(new JsonArray("success"), new JsonArray(CourseMutation(1), CourseMutation(2), CourseMutation(3))),
        ["resolution"] = new JsonObject { ["clockKind"] = "world_time.currentTimeInMinutes", ["maximumGapMinutes"] = 600 },
        ["outcomes"] = new JsonArray(
            CourseMilestone(1, 0, "active", new JsonArray()),
            CourseMilestone(2, 480, "active", new JsonArray(new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 })),
            CourseMilestone(3, 960, "completed", new JsonArray(new JsonObject { ["kind"] = "heal", ["legacies"] = new JsonArray() }))),
        ["interruption"] = new JsonObject { ["category"] = "failed_attempt", ["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" }) }
    };

    private static JsonObject StrictGuaranteedRoute() => new()
    {
        ["routeId"] = "guaranteed_v1", ["displayName"] = "Guaranteed", ["visibility"] = "known_to_player", ["mode"] = "guaranteed",
        ["requirements"] = new JsonArray(new JsonObject { ["kind"] = "source_capability", ["capabilityRef"] = "exact_materialized_healing_source", ["actorRole"] = "provider" }),
        ["resourcePolicy"] = Policy(new JsonArray("success"), new JsonArray()),
        ["resolution"] = new JsonObject { ["capabilityRef"] = "exact_materialized_healing_source", ["actorRole"] = "provider" },
        ["outcomes"] = new JsonArray(new JsonObject { ["category"] = "success", ["result"] = new JsonArray(new JsonObject { ["kind"] = "stabilize" }) }), ["interruption"] = null
    };

    private static JsonObject Policy(JsonArray consumeOn, JsonArray mutations) => new() { ["reserveBeforeResolution"] = true, ["consumeOn"] = consumeOn, ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"), ["mutations"] = mutations };
    private static JsonObject Band(string id, int? minimum, int? maximum, string category, JsonArray result) => new() { ["bandId"] = id, ["minimumMargin"] = minimum, ["maximumMargin"] = maximum, ["category"] = category, ["result"] = result };
    private static JsonObject CourseMutation(int ordinal) => new() { ["kind"] = "consume_requirement", ["scope"] = "course_milestone", ["milestoneOrdinal"] = ordinal, ["requirementIndex"] = 0 };
    private static JsonObject CourseMilestone(int ordinal, long afterMinutes, string completion, JsonArray result) => new() { ["ordinal"] = ordinal, ["afterMinutes"] = afterMinutes, ["requirements"] = new JsonArray(new JsonObject { ["kind"] = "item_quantity", ["itemRef"] = "antibiotic_dose", ["quantity"] = 1, ["ownerRole"] = "target" }), ["category"] = "success", ["completion"] = completion, ["result"] = result };
    private static EffectRoots CreateEffectRoots(ResolverScenario scenario)
    {
        var effects = new List<JsonObject>
        {
            // Carrier order must not choose the shield: newer deliberately precedes
            // older, while the production arbiter still chooses createdAtTurn 37.
            CreateMaterializedFateShield("effect_fate_shield_newer", 42),
            CreateMaterializedFateShield("effect_fate_shield_older", 37)
        };
        if (scenario.RollMode is "advantage" or "disadvantage")
            effects.Add(CreateRollModeEffect(scenario.RollMode));
        return new EffectRoots(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effects.Select(static effect => (JsonNode)effect).ToArray())
            },
            CreateEffectIdentityIndex(effects));
    }

    private static JsonObject CreateMaterializedFateShield(string effectId, int createdAtTurn)
    {
        // Let the accepted-turn effect planner materialize the built-in source.  It
        // supplies the full uses lifetime (including consuming trigger IDs) and the
        // canonical built-in component/trigger topology; the fixture changes only
        // independent carrier identity and chronology afterwards.
        var planned = BuildFateShieldPlan();
        Assert.True(planned.Success, DescribeIssues(planned.Issues));
        var effect = Assert.Single(planned.Plan!.ActiveEffects).DeepClone().AsObject();
        effect["effectId"] = effectId;
        effect["chronology"]!["createdAtTurn"] = createdAtTurn;
        effect["chronology"]!["lastTransitionTurn"] = createdAtTurn;
        return effect;
    }

    private static EffectAcceptedTurnPlanningResult BuildFateShieldPlan()
    {
        var commands = new JsonObject
        {
            ["effectChanges"] = new JsonArray(new JsonObject
            {
                ["operation"] = "apply",
                ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
                ["source"] = new JsonObject
                {
                    ["kind"] = EffectBuiltInSourceCatalog.FateShieldSourceKind,
                    ["sourceId"] = EffectBuiltInSourceCatalog.FateShieldSourceId,
                    ["definitionKey"] = EffectBuiltInSourceCatalog.FateShieldDefinitionKey
                },
                ["parameters"] = new JsonObject(),
                ["eventRef"] = new JsonObject { ["kind"] = "accepted_turn", ["authorityId"] = "turn_42" },
                ["reason"] = "T061 canonical Fate Shield fixture."
            }),
            ["effectResolutionReceipts"] = new JsonArray()
        };
        var input = new EffectAcceptedTurnInput(
            "session_t061_fate",
            "snapshot_t061_fate",
            commands,
            EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
                EffectBuiltInSourceCatalog.CreateCanonicalExports(),
                Array.Empty<EffectSourceExport>(),
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal)
                {
                    EffectBuiltInSourceCatalog.FateShieldApplicationAuthority
                })),
            EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
                new[] { new EffectTargetExport("mortal_world", "player", "player_current", SameTurn: false) },
                Array.Empty<EffectTargetExport>(),
                new HashSet<string>(StringComparer.Ordinal),
                null)),
            new JsonObject
            {
                ["turn"] = 42,
                ["events"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "accepted_turn", ["authorityId"] = "turn_42",
                    ["eventRef"] = "turn_42:accepted_effect"
                }),
                ["lifecycleEvents"] = new JsonArray()
            },
            PreTurnCarriers: new EffectCarrierCatalogInput(null, null, null, null, null, null));
        return EffectAcceptedTurnPlanner.Build(
            input,
            "t061_fate_shield_fixture",
            new EffectIdentityFactory());
    }

    private static JsonObject CreateRollModeEffect(string rollMode)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("player", "roll_modifier");
        effect["effectId"] = "effect_roll_modifier_" + rollMode;
        effect["components"]![0]!["payload"] = new JsonObject
        {
            ["operations"] = new JsonArray("skill_check"),
            ["contribution"] = rollMode
        };
        return effect;
    }

    private static JsonObject CreateEffectIdentityIndex(IEnumerable<JsonObject> effects)
    {
        var rows = effects.ToArray();
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(rows);
        var entries = index["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        for (var ordinal = 0; ordinal < entries.Length; ordinal++)
        {
            var effect = rows[ordinal];
            var createdAtTurn = effect["chronology"]!["createdAtTurn"]!.GetValue<int>();
            var eventRef = $"turn_{createdAtTurn}:fate_shield_created:{ordinal + 1}";
            var transitionId = $"effect_transition_fate_shield_{ordinal + 1}";
            entries[ordinal]["createdAtTurn"] = createdAtTurn;
            var transition = entries[ordinal]["transitions"]![0]!.AsObject();
            transition["transitionId"] = transitionId;
            transition["turn"] = createdAtTurn;
            transition["eventRef"] = eventRef;
            effect["chronology"]!["createdEventRef"] = eventRef;
            effect["chronology"]!["lastTransitionId"] = transitionId;
        }
        return index;
    }

    private static JsonObject CreateCanonicalStack(string itemId, int count)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot(itemId);
        item["count"] = count;
        MortalItemTestFixture.ResealCanonical(item);
        return item;
    }

    private static JsonObject PlayerInventoryRoot(JsonObject? dose) => new()
    {
        ["items"] = dose == null ? new JsonArray() : new JsonArray(dose)
    };

    private static JsonObject NpcCoreRoot(JsonObject sterileThread) => new()
    {
        // MortalItemCarrierCatalog deliberately accepts only UpdateNPCs and
        // NPCsInScene for canonical NPC inventories.  Keep the item in the latter
        // rather than an ignored compatibility section.
        ["NPCsInScene"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "field_medic_01",
            ["inventory"] = new JsonArray(sterileThread)
        })
    };

    private static JsonObject CreateTreatmentSkill(string skillId, string capabilityRef) => new()
    {
        // This is the smallest current production-valid active-skill envelope
        // from ActorMaterializationContractTests, extended only by T060's
        // materialized treatment capability surface.
        ["skillId"] = skillId,
        ["displayName"] = "Field Medicine",
        ["lifecycle"] = "active",
        ["active"] = true,
        ["tier"] = 3,
        ["skillName"] = "Field Medicine",
        ["skillDescription"] = "Provides precise field care under pressure.",
        ["rarity"] = "Common",
        ["actionCost"] = "Main",
        ["combatEffect"] = new JsonObject
        {
            ["isActivatedEffect"] = true,
            ["actionName"] = "Field treatment",
            ["effects"] = new JsonArray(new JsonObject
            {
                ["effectType"] = "Damage",
                ["value"] = "10%",
                ["targetType"] = "Enemy",
                ["effectDescription"] = "A controlled intervention.",
                ["poiseDamage"] = "5%"
            })
        },
        ["mortalWoundTreatmentCapabilities"] = new JsonArray(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["capabilityRef"] = capabilityRef,
            ["woundDomain"] = "physical",
            ["minimumSeverityRank"] = 1,
            ["maximumSeverityRank"] = 4,
            ["operationLimits"] = new JsonObject
            {
                ["mayStabilize"] = true,
                ["maximumRecoveryPoints"] = 2,
                ["maximumSeverityReductionSteps"] = 1,
                ["removableComplicationKinds"] = new JsonArray("infection"),
                ["mayHealAtSeverityI"] = true,
                ["maximumCosmeticHealLegacies"] = 1,
                ["maximumMechanicalEffectHealLegacies"] = 1
            }
        })
    };

    private static JsonObject CreateHistoryFor(string name, string mode)
    {
        return WoundContractTestData.CreateHistory();
    }

    private static void ExecutePreparedFlow(ResolverScenario scenario)
    {
        // The absent planner must stay the uniform current RED boundary.  Once it
        // exists, every descriptor reaches a strict mode-specific wound/history.
        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            PlannerTypeName,
            throwOnError: false,
            ignoreCase: false);
        Assert.True(planner is not null,
            $"T061 planner is absent; scenario '{scenario.Name}' cannot enter the production-only accepted-state path.");

        // Establish concrete scenario inputs after the current RED seam.  In
        // particular, no raw authority object, die choice, proof, request, receipt,
        // bundle, reservation, or fingerprint is test-constructed here.
        var before = WoundMaterializationContract.Parse(
            scenario.Before.ToJsonString(),
            "wound");
        var history = WoundHistoryState.Parse(
            scenario.History.ToJsonString(),
            "history");
        Assert.True(before.IsValid, DescribeIssues(before.Issues));
        Assert.NotNull(before.Wound);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.NotNull(history.State);
        Assert.Equal("mortal_world", scenario.AcceptedState["realm"]!.GetValue<string>());
        Assert.Equal("physical", before.Wound!.Classification.Domain);
        Assert.Equal(scenario.EventRef, scenario.AcceptedState["eventRef"]!.GetValue<string>());

        var factoryName = scenario.Mode switch
        {
            "procedure" => "PrepareProcedureRequest",
            "course" => "PrepareCourseMilestoneRequest",
            "guaranteed" => "PrepareGuaranteedRequest",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario.Mode), scenario.Mode, null)
        };
        var resolverName = scenario.Mode switch
        {
            "procedure" => "CreateProcedureAttempt",
            "course" => "CreateCourseMilestoneAttempt",
            "guaranteed" => "CreateGuaranteedAttempt",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario.Mode), scenario.Mode, null)
        };
        var factory = ExactStaticMethod(planner, factoryName, 6);
        Assert.Equal("MortalWoundTreatmentAttemptRequestResult", factory.ReturnType.Name);
        Assert.Equal(typeof(string), factory.GetParameters()[3].ParameterType);
        Assert.Equal(typeof(string), factory.GetParameters()[4].ParameterType);
        Assert.Equal(typeof(string), factory.GetParameters()[5].ParameterType);
        Assert.False(typeof(JsonNode).IsAssignableFrom(factory.GetParameters()[0].ParameterType));
        Assert.False(typeof(JsonNode).IsAssignableFrom(factory.GetParameters()[1].ParameterType));
        Assert.False(typeof(JsonNode).IsAssignableFrom(factory.GetParameters()[2].ParameterType));
        AssertFutureTypedHandoffs(planner, scenario.Mode);

        // The fixture passes real canonical sources to the registry and obtains the
        // opaque accepted state under one lease.  It does not create an authority,
        // request, proof, witness, reservation, receipt, or fingerprint itself.
        using var fixture = AcceptedStateFixture.Create(scenario);
        if (scenario.Mode == "procedure")
        {
            fixture.AssertUnchangedT060RequirementResolution(
                Assert.Single(before.Wound!.Treatment.Routes));
        }
        var acceptedState = fixture.GetAcceptedState();
        if (scenario.Name == "course_unsatisfied_dose_requirement_has_no_reservation")
        {
            AssertCourseUnsatisfiedRequirementSeam(
                planner,
                acceptedState,
                history,
                before.Wound!,
                scenario);
        }
        var preparedResult = Invoke(factory, new object?[]
        {
            acceptedState,
            history,
            before.Wound,
            scenario.OperationKey,
            scenario.RouteId,
            scenario.EventRef
        });
        if (scenario.ExpectedBoundary == "PreparationRejected")
        {
            AssertInvalidTypedResult(preparedResult, "Request", scenario.Name + " request");
            return;
        }

        var request = ReadValidTypedResult(preparedResult, "Request", scenario.Name + " request");
        if (scenario.Mode == "procedure")
        {
            AssertProcedureCheckAuthority(
                ReadRequiredProperty(request, "ModeAuthority"),
                scenario);
            AssertPreparedCriticalReaction(request, acceptedState, scenario);
        }
        var resolver = ExactStaticMethod(planner, resolverName, 4);
        var resolution = Invoke(resolver, new[]
        {
            request,
            (object?)history,
            before.Wound,
            acceptedState
        });
        AssertResolutionShape(resolution, scenario, request);
    }

    private static void AssertPreparedCriticalReaction(
        object request,
        object acceptedState,
        ResolverScenario scenario)
    {
        var catalogType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.EffectAcceptedEventReportCatalog",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(catalogType);
        var result = Invoke(
            ExactStaticMethod(catalogType, "ResolvePreparedMortalWoundCriticalReaction", 2),
            new[] { request, acceptedState });
        Assert.Equal(
            new[] { "IsValid", "Issues", "Intent" },
            result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static property => property));
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Empty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(result, "Issues")).Cast<object>());
        var intent = ReadPropertyAllowingNull(result, "Intent");
        if (scenario.ExpectedFateEffectId == null)
        {
            Assert.Null(intent);
            return;
        }

        Assert.NotNull(intent);
        AssertClosedProperties(intent!, new[]
        {
            "EventType", "EventRef", "CausalEventRef", "Turn", "Realm", "TargetKind", "TargetId",
            "EffectId", "TriggerId", "AcceptedEffectFingerprint", "PreparedReactionFingerprint",
            "RequestFingerprint", "IntentFingerprint"
        });
        Assert.Equal("owner_critical_failure", Convert.ToString(ReadRequiredProperty(intent, "EventType")));
        Assert.Equal("mortal_world", Convert.ToString(ReadRequiredProperty(intent, "Realm")));
        Assert.Equal("player", Convert.ToString(ReadRequiredProperty(intent, "TargetKind")));
        Assert.Equal("player_current", Convert.ToString(ReadRequiredProperty(intent, "TargetId")));
        Assert.Equal(scenario.ExpectedFateEffectId, Convert.ToString(ReadRequiredProperty(intent, "EffectId")));
        Assert.Equal("fate_shield_on_critical_failure", Convert.ToString(ReadRequiredProperty(intent, "TriggerId")));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(intent, "AcceptedEffectFingerprint"))));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(intent, "PreparedReactionFingerprint"))));
    }

    private static void AssertCourseUnsatisfiedRequirementSeam(
        Type planner,
        object acceptedState,
        WoundHistoryParseResult history,
        WoundMaterializationEnvelope before,
        ResolverScenario scenario)
    {
        // This follows the production course chain instead of inferring a missing
        // dose from the high-level preparation failure.  Each authority is created
        // by its owning future factory; no coordinates, clock, bundle, or claim is
        // test-constructed.
        var coordinates = ReadValidTypedResult(
            Invoke(ExactStaticMethod(planner, "CreateAttemptCoordinates", 5), new object?[]
            {
                acceptedState, before, scenario.OperationKey, scenario.RouteId, scenario.EventRef
            }),
            "Coordinates",
            scenario.Name + " coordinates");
        var gameTimeType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundGameTimeAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(gameTimeType);
        var gameTime = ReadValidTypedResult(
            Invoke(ExactStaticMethod(gameTimeType, "Create", 2), new[] { acceptedState, coordinates }),
            "Authority",
            scenario.Name + " game time");
        var courseModeType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundCourseModeAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(courseModeType);
        var courseMode = AssertReadyCourseModeAuthority(
            Invoke(ExactStaticMethod(courseModeType, "Create", 5), new[]
            {
                acceptedState, coordinates, (object)before, history, gameTime
            }),
            scenario);
        var bundleType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentRequirementAuthorityBundle",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(bundleType);
        var requirement = Invoke(
            ExactStaticMethod(bundleType, "CreateForCourseMilestone", 5),
            new[] { acceptedState, coordinates, (object)before, history, courseMode });
        AssertClosedProperties(requirement, new[] { "Status", "Issues", "Authority" });
        Assert.Equal("Unsatisfied", Convert.ToString(ReadRequiredProperty(requirement, "Status")));
        var bundle = ReadPropertyAllowingNull(requirement, "Authority");
        Assert.NotNull(bundle);
        AssertCourseUnsatisfiedBundle(bundle!);

        var resourceComposer = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentResourceComposer",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(resourceComposer);
        var preparation = Invoke(ExactStaticMethod(resourceComposer, "PrepareCourse", 5), new[]
        {
            acceptedState, coordinates, (object)before, bundle, courseMode
        });
        // At first milestone, trustworthy Unsatisfied must reject before a
        // reservation exists.  The null authority therefore proves no claim/ID can
        // be observed or persisted, while the prior status assertion rules out
        // InvalidAuthority accidentally taking this branch.
        AssertInvalidTypedResult(preparation, "Authority", scenario.Name + " no-reservation preparation");
    }

    private static object AssertReadyCourseModeAuthority(object result, ResolverScenario scenario)
    {
        AssertClosedProperties(result, new[] { "Disposition", "Issues", "Authority" });
        Assert.Equal("Ready", Convert.ToString(ReadRequiredProperty(result, "Disposition")));
        Assert.Empty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(result, "Issues")).Cast<object>());
        var authority = ReadPropertyAllowingNull(result, "Authority");
        Assert.NotNull(authority);
        AssertClosedProperties(authority!, new[]
        {
            "GameTimeAuthority", "CourseId", "MilestoneOrdinal", "DueAtGameTimeMinutes",
            "DeadlineAtGameTimeMinutes", "WindowDisposition", "CourseStartAuthority",
            "CourseCoordinateFingerprint", "CoordinatesFingerprint", "AcceptedStateFingerprint",
            "AuthorityFingerprint"
        });
        Assert.NotNull(ReadRequiredProperty(authority, "CourseId"));
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(authority, "MilestoneOrdinal")));
        Assert.Equal(scenario.WorldMinute,
            Convert.ToInt64(ReadRequiredProperty(authority, "DueAtGameTimeMinutes")));
        Assert.Equal("ready", Convert.ToString(ReadRequiredProperty(authority, "WindowDisposition")));
        var start = ReadRequiredProperty(authority, "CourseStartAuthority");
        Assert.NotNull(start);
        Assert.NotNull(ReadRequiredProperty(start, "StartingWound"));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(start, "AuthorityFingerprint"))));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(authority, "CourseCoordinateFingerprint"))));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(authority, "AuthorityFingerprint"))));
        return authority!;
    }

    private static void AssertCourseUnsatisfiedBundle(object bundle)
    {
        AssertClosedProperties(bundle, new[]
        {
            "Mode", "ContextFingerprint", "AcceptedStateFingerprint", "RouteFingerprint", "CourseId",
            "CourseMilestoneOrdinal", "CourseCoordinateFingerprint", "CourseRequirementStatus",
            "InterruptionReason", "Scopes", "AuthorityFingerprint"
        });
        Assert.Equal("course", Convert.ToString(ReadRequiredProperty(bundle, "Mode")));
        Assert.Equal("Unsatisfied", Convert.ToString(
            ReadRequiredProperty(bundle, "CourseRequirementStatus")));
        Assert.Null(ReadPropertyAllowingNull(bundle, "InterruptionReason"));
        Assert.NotNull(ReadRequiredProperty(bundle, "CourseId"));
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(bundle, "CourseMilestoneOrdinal")));
        var scopes = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(bundle, "Scopes")).Cast<object>().ToArray();
        Assert.Equal(2, scopes.Length);
        var common = Assert.Single(scopes, scope =>
            string.Equals(Convert.ToString(ReadRequiredProperty(scope, "Scope")), "common", StringComparison.Ordinal));
        var milestone = Assert.Single(scopes, scope =>
            string.Equals(Convert.ToString(ReadRequiredProperty(scope, "Scope")), "course_milestone", StringComparison.Ordinal));
        Assert.Equal("Satisfied", Convert.ToString(ReadRequiredProperty(common, "Status")));
        Assert.Equal("Unsatisfied", Convert.ToString(ReadRequiredProperty(milestone, "Status")));
        Assert.Empty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(milestone, "Bindings")).Cast<object>());
        Assert.NotEmpty(Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(milestone, "FailureWitnesses")).Cast<object>());
    }

    private static MethodInfo ExactStaticMethod(Type type, string name, int parameterCount) =>
        Assert.Single(
            type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => candidate.Name == name && candidate.GetParameters().Length == parameterCount);

    private static MethodInfo ExactInstanceMethod(Type type, string name, int parameterCount) =>
        Assert.Single(
            type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => candidate.Name == name && candidate.GetParameters().Length == parameterCount);

    private static void AssertFutureTypedHandoffs(Type planner, string mode)
    {
        var t060 = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(t060);
        var unchangedRequirements = ExactStaticMethod(t060, "ResolveRequirements", 3);
        Assert.Equal(typeof(WoundTreatmentRoute), unchangedRequirements.GetParameters()[0].ParameterType);
        Assert.False(typeof(JsonNode).IsAssignableFrom(unchangedRequirements.GetParameters()[1].ParameterType));
        Assert.False(typeof(JsonNode).IsAssignableFrom(unchangedRequirements.GetParameters()[2].ParameterType));
        var sealName = mode switch
        {
            "procedure" => "SealProcedureRequest",
            "course" => "SealCourseMilestoneRequest",
            "guaranteed" => "SealGuaranteedRequest",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        var seal = ExactStaticMethod(planner, sealName, 4);
        Assert.Equal("MortalWoundTreatmentAttemptRequestResult", seal.ReturnType.Name);

        var bundleType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentRequirementAuthorityBundle",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(bundleType);
        var bundleName = mode switch
        {
            "procedure" => "CreateForProcedure",
            "course" => "CreateForCourseMilestone",
            "guaranteed" => "CreateForGuaranteed",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        var bundle = ExactStaticMethod(bundleType, bundleName, mode == "course" ? 5 : 3);
        Assert.False(typeof(JsonNode).IsAssignableFrom(bundle.GetParameters()[0].ParameterType));

        var historyProbe = ExactInstanceMethod(
            typeof(WoundHistoryParseResult),
            "ProbeTreatmentAttempt",
            3);
        Assert.Equal("MortalWoundTreatmentReplayProbeResult", historyProbe.ReturnType.Name);

        var reactionCatalog = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.EffectAcceptedEventReportCatalog",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(reactionCatalog);
        var reaction = ExactStaticMethod(
            reactionCatalog,
            "ResolvePreparedMortalWoundCriticalReaction",
            2);
        Assert.Equal("MortalWoundCriticalReactionResolutionResult", reaction.ReturnType.Name);
        var compose = ExactStaticMethod(reactionCatalog, "Compose", 5);
        Assert.Equal(typeof(JsonNode), compose.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(int), compose.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(string), compose.GetParameters()[2].ParameterType);

        var resourceComposer = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentResourceComposer",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(resourceComposer);
        var resourceName = mode switch
        {
            "procedure" => "PrepareProcedure",
            "course" => "PrepareCourse",
            "guaranteed" => "PrepareGuaranteed",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        Assert.Equal(
            "MortalWoundTreatmentResourcePreparationResult",
            ExactStaticMethod(resourceComposer, resourceName, 5).ReturnType.Name);
        Assert.Equal(
            "MortalWoundTreatmentResourceFinalizationResult",
            ExactStaticMethod(resourceComposer, "Finalize", 1).ReturnType.Name);

    }

    private static object Invoke(MethodInfo method, object?[] arguments)
    {
        try
        {
            var result = method.Invoke(null, arguments);
            Assert.NotNull(result);
            return result;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static object ReadValidTypedResult(object result, string propertyName, string boundary)
    {
        Assert.Equal(
            new[] { "IsValid", "Issues", propertyName }.OrderBy(static value => value),
            result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static value => value));
        var valid = Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid"));
        var issues = ReadRequiredProperty(result, "Issues") as System.Collections.IEnumerable;
        Assert.NotNull(issues);
        Assert.True(valid, $"{boundary} was rejected by the production boundary.");
        Assert.Empty(issues.Cast<object>());
        return ReadRequiredProperty(result, propertyName);
    }

    private static void AssertInvalidTypedResult(object result, string propertyName, string boundary)
    {
        Assert.Equal(
            new[] { "IsValid", "Issues", propertyName }.OrderBy(static value => value),
            result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static value => value));
        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")),
            $"{boundary} unexpectedly crossed its explicit rejection boundary.");
        var issues = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(result, "Issues")).Cast<object>().ToArray();
        Assert.NotEmpty(issues);
        Assert.Null(ReadPropertyAllowingNull(result, propertyName));
    }

    private static void AssertResolutionShape(
        object result,
        ResolverScenario scenario,
        object request)
    {
        Assert.Equal(
            new[] { "Disposition", "Issues", "ReplayReceipt", "Resolution" },
            result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static value => value));
        var disposition = Assert.IsType<string>(ReadRequiredProperty(result, "Disposition"));
        var issues = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(result, "Issues")).Cast<object>().ToArray();
        Assert.Empty(issues);
        Assert.Equal("Resolved", disposition);
        Assert.NotNull(ReadPropertyAllowingNull(result, "Resolution"));
        Assert.Null(ReadPropertyAllowingNull(result, "ReplayReceipt"));
        var resolved = ReadRequiredProperty(result, "Resolution");
        AssertClosedProperties(resolved, new[]
        {
            "Mode", "Coordinates", "AttemptDisposition", "ResultCategory", "SelectedOutcomeIndex",
            "Interruption", "DeclaredResult", "OutcomeIntents", "CriticalReactionIntent",
            "ConsumptionTrigger", "CourseId", "CourseMilestoneOrdinal", "CourseDisposition",
            "RequestAuthority", "RequirementAuthority", "ResourceAuthority", "ModeEvidence",
            "RouteFingerprint", "ResolutionAuthorityFingerprint", "RequestFingerprint",
            "ResultFingerprint", "RouteCompletion"
        });
        Assert.Equal(scenario.Mode, Assert.IsType<string>(ReadRequiredProperty(resolved, "Mode")));
        Assert.Equal("AcceptedTerminal", Assert.IsType<string>(ReadRequiredProperty(resolved, "AttemptDisposition")));
        Assert.Equal(scenario.ExpectedCategory,
            Assert.IsType<string>(ReadRequiredProperty(resolved, "ResultCategory")));
        Assert.Equal(request.GetType(), ReadRequiredProperty(resolved, "RequestAuthority").GetType());
        AssertCompleteRequestBundle(ReadRequiredProperty(resolved, "RequestAuthority"), scenario);
        if (scenario.Mode == "procedure")
            AssertProcedureCheckAuthority(ReadRequiredProperty(resolved, "ModeEvidence"), scenario);
        var intents = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(resolved, "OutcomeIntents")).Cast<object>().ToArray();
        Assert.Equal(scenario.ExpectedIntentCount, intents.Length);
        Assert.All(intents, intent =>
        {
            Assert.NotNull(ReadPropertyAllowingNull(intent, "IntentFingerprint"));
            Assert.NotNull(ReadPropertyAllowingNull(intent, "Kind"));
        });
    }

    private static void AssertCompleteRequestBundle(object request, ResolverScenario scenario)
    {
        AssertClosedProperties(request, new[]
        {
            "Mode", "Coordinates", "MilestoneOrdinal", "ModeAuthority", "RequirementAuthority",
            "ResourceAuthority", "RequestFingerprint"
        });
        Assert.Equal(scenario.Mode, Assert.IsType<string>(ReadRequiredProperty(request, "Mode")));
        Assert.NotNull(ReadRequiredProperty(request, "Coordinates"));
        Assert.NotNull(ReadRequiredProperty(request, "ModeAuthority"));
        Assert.NotNull(ReadRequiredProperty(request, "RequirementAuthority"));
        Assert.NotNull(ReadRequiredProperty(request, "ResourceAuthority"));
        Assert.NotNull(ReadRequiredProperty(request, "RequestFingerprint"));
        var bundle = ReadRequiredProperty(request, "RequirementAuthority");
        AssertClosedProperties(bundle, new[]
        {
            "Mode", "ContextFingerprint", "AcceptedStateFingerprint", "RouteFingerprint", "CourseId",
            "CourseMilestoneOrdinal", "CourseCoordinateFingerprint", "CourseRequirementStatus",
            "InterruptionReason", "Scopes", "AuthorityFingerprint"
        });
        Assert.Equal(scenario.Mode, Assert.IsType<string>(ReadRequiredProperty(bundle, "Mode")));
        Assert.NotNull(ReadRequiredProperty(bundle, "AuthorityFingerprint"));
        var scopes = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(bundle, "Scopes")).Cast<object>().ToArray();
        Assert.NotEmpty(scopes);
        Assert.All(scopes, scope =>
        {
            AssertClosedProperties(scope, new[]
            {
                "Scope", "CourseMilestoneOrdinal", "Status", "Bindings", "FailureWitnesses",
                "AuthorityFingerprint"
            });
            Assert.NotNull(ReadRequiredProperty(scope, "Status"));
            Assert.NotNull(ReadRequiredProperty(scope, "AuthorityFingerprint"));
        });
        if (scenario.Mode == "course")
        {
            Assert.NotNull(ReadRequiredProperty(request, "MilestoneOrdinal"));
            Assert.NotNull(ReadRequiredProperty(bundle, "CourseId"));
            Assert.NotNull(ReadRequiredProperty(bundle, "CourseMilestoneOrdinal"));
        }
    }

    private static void AssertProcedureCheckAuthority(object authority, ResolverScenario scenario)
    {
        AssertClosedProperties(authority, new[]
        {
            "SourcePath", "RollMode", "RollActorKind", "RollActorId", "RollContributions",
            "SourceIndices", "SourceRolls", "SelectedSourceIndex", "NaturalRoll", "Modifier",
            "ComplicationDifficultyModifier", "EffectiveDifficulty", "RequirementAuthorityFingerprint",
            "CoordinatesFingerprint", "AcceptedStateFingerprint", "PreparedCriticalReaction",
            "AuthorityFingerprint"
        });
        Assert.Equal("input/turn_request.json",
            Convert.ToString(ReadRequiredProperty(authority, "SourcePath")));
        Assert.Equal(scenario.RollMode, Convert.ToString(ReadRequiredProperty(authority, "RollMode")));
        Assert.Equal("player", Convert.ToString(ReadRequiredProperty(authority, "RollActorKind")));
        Assert.Equal("player_current", Convert.ToString(ReadRequiredProperty(authority, "RollActorId")));
        var contributions = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            ReadRequiredProperty(authority, "RollContributions")).Cast<object>().ToArray();
        if (scenario.RollMode == "normal")
        {
            Assert.Empty(contributions);
        }
        else
        {
            var contribution = Assert.Single(contributions);
            AssertClosedProperties(contribution, new[] { "EffectId", "ComponentId", "Contribution" });
            Assert.Equal("effect_roll_modifier_" + scenario.RollMode,
                Convert.ToString(ReadRequiredProperty(contribution, "EffectId")));
            Assert.Equal("component_001",
                Convert.ToString(ReadRequiredProperty(contribution, "ComponentId")));
            Assert.Equal(scenario.RollMode,
                Convert.ToString(ReadRequiredProperty(contribution, "Contribution")));
        }
        Assert.Equal(scenario.ExpectedSourceIndices,
            ReadIntSequence(ReadRequiredProperty(authority, "SourceIndices")));
        var acceptedDice = scenario.AcceptedState["acceptedDice"]!.AsArray();
        Assert.Equal(
            scenario.ExpectedSourceIndices.Select(index => acceptedDice[index]!.GetValue<int>()),
            ReadIntSequence(ReadRequiredProperty(authority, "SourceRolls")));
        Assert.Equal(scenario.ExpectedSelectedSourceIndex,
            Convert.ToInt32(ReadRequiredProperty(authority, "SelectedSourceIndex")));
        Assert.Equal(scenario.ExpectedNaturalRoll,
            Convert.ToInt32(ReadRequiredProperty(authority, "NaturalRoll")));
        var prepared = ReadPropertyAllowingNull(authority, "PreparedCriticalReaction");
        if (scenario.ExpectedFateEffectId == null)
        {
            Assert.Null(prepared);
            return;
        }

        Assert.NotNull(prepared);
        AssertClosedProperties(prepared!, new[]
        {
            "EffectId", "TriggerId", "AcceptedEffectFingerprint", "PreparedReactionFingerprint"
        });
        Assert.Equal(scenario.ExpectedFateEffectId,
            Convert.ToString(ReadRequiredProperty(prepared!, "EffectId")));
        Assert.Equal("fate_shield_on_critical_failure",
            Convert.ToString(ReadRequiredProperty(prepared, "TriggerId")));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(prepared, "AcceptedEffectFingerprint"))));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(prepared, "PreparedReactionFingerprint"))));
    }

    private static int[] ReadIntSequence(object value) =>
        Assert.IsAssignableFrom<System.Collections.IEnumerable>(value)
            .Cast<object>()
            .Select(Convert.ToInt32)
            .ToArray();

    private static void AssertClosedProperties(object value, IEnumerable<string> expected) =>
        Assert.Equal(
            expected.OrderBy(static name => name),
            value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static name => name));

    private static object ReadRequiredProperty(object instance, string name)
    {
        var value = ReadPropertyAllowingNull(instance, name);
        Assert.NotNull(value);
        return value;
    }

    private static object ReadRequiredProperty(object?[] arguments, int index, string boundary)
    {
        var value = arguments[index];
        Assert.NotNull(value);
        return value;
    }

    private static object? ReadPropertyAllowingNull(object instance, string name)
    {
        var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return property.GetValue(instance);
    }

    private static string DescribeIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(" | ", issues.Select(issue => $"{issue.Code}@{issue.FilePath}"));

    private sealed record ResolverScenario(
        string Name,
        string Mode,
        JsonObject AcceptedState,
        JsonObject History,
        JsonObject Before,
        string OperationKey,
        string RouteId,
        string EventRef,
        string ExpectedBoundary,
        string? ExpectedCategory,
        string RollMode,
        int[] ExpectedSourceIndices,
        int ExpectedSelectedSourceIndex,
        int ExpectedNaturalRoll,
        long WorldMinute,
        bool RequirementsAvailable,
        int ExpectedIntentCount,
        string? ExpectedFateEffectId);

    private sealed record ScenarioSemantics(
        string RollMode,
        int[] SourceIndices,
        int SelectedSourceIndex,
        int NaturalRoll,
        long WorldMinute,
        bool RequirementsAvailable,
        string ExpectedBoundary,
        string? ExpectedCategory,
        int ExpectedIntentCount,
        string? ExpectedFateEffectId);

    private sealed record EffectRoots(JsonObject PlayerEffects, JsonObject IdentityIndex);

    private sealed class AcceptedStateFixture : IDisposable
    {
        private AcceptedStateFixture(
            string root,
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease,
            WoundAcceptedTurnBinding binding,
            JsonObject context,
            string woundId)
        {
            Root = root;
            FileSystem = fileSystem;
            Lease = lease;
            Binding = binding;
            Context = context;
            WoundId = woundId;
        }

        private string Root { get; }
        private FileSystemManager FileSystem { get; }
        private FileSystemManager.CanonicalWriteLease Lease { get; }
        private WoundAcceptedTurnBinding Binding { get; }
        private JsonObject Context { get; }
        private string WoundId { get; }

        internal static AcceptedStateFixture Create(ResolverScenario scenario)
        {
            var root = Path.Combine(Path.GetTempPath(), "boe-t061-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var fileSystem = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fileSystem.EnsureDirectoryStructure();
            try
            {
                foreach (var relativePath in new[]
                         {
                             "game_state/world/world_time.json",
                             "input/turn_request.json",
                             EffectCarrierCatalog.PlayerPath,
                             EffectIdentityState.StatePath,
                             WoundIdentityState.StatePath,
                             WoundHistoryState.HistoryPath,
                             "game_state/inventory/items.json",
                             "game_state/inventory/item_identity_index.json"
                         })
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(
                        fileSystem.ResolvePath(relativePath))!);
                }
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/player/wounds.json"),
                    WoundContractTestData.CreatePlayerCarrier(scenario.Before).ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath(WoundIdentityState.StatePath),
                    WoundContractTestData.CreateIdentityIndex(
                        WoundContractTestData.CreateIdentityEntry()).ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath(WoundHistoryState.HistoryPath),
                    scenario.History.ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/player/skills_active.json"),
                    new JsonObject
                    {
                        ["activeSkillChanges"] = new JsonArray(
                            CreateTreatmentSkill("skill_field_medicine_01", "field_medicine"))
                    }.ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/player/skills_passive.json"),
                    new JsonObject { ["passiveSkillChanges"] = new JsonArray() }.ToJsonString());
                var sterileThread = CreateCanonicalStack("sterile_thread", 2);
                var dose = scenario.RequirementsAvailable
                    ? CreateCanonicalStack("antibiotic_dose", 1)
                    : null;
                var npcCore = NpcCoreRoot(sterileThread);
                npcCore["NPCsInScene"]![0]!["activeSkills"] = new JsonArray(
                    CreateTreatmentSkill("skill_guaranteed_care_01", "exact_materialized_healing_source"));
                npcCore["NPCsInScene"]![0]!["passiveSkills"] = new JsonArray();
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/npcs/npc_core.json"),
                    npcCore.ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/world/world_time.json"),
                    new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["currentTimeInMinutes"] = scenario.AcceptedState["worldMinute"]!.DeepClone()
                    }.ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/inventory/items.json"),
                    PlayerInventoryRoot(dose).ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/inventory/item_identity_index.json"),
                    dose == null
                        ? MortalItemTestFixture.CreateIndexForCarrier(
                            sterileThread,
                            "npc_inventory",
                            "field_medic_01").ToJsonString()
                        : MortalItemTestFixture.CreateIndexForCarriers(
                            (sterileThread, "npc_inventory", "field_medic_01", null),
                            (dose, "player_inventory", "player", null)).ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("input/turn_request.json"),
                    new JsonObject
                    {
                        ["sessionId"] = scenario.AcceptedState["sessionId"]!.DeepClone(),
                        ["requestId"] = scenario.AcceptedState["requestId"]!.DeepClone(),
                        ["turnNumber"] = scenario.AcceptedState["turn"]!.DeepClone(),
                        ["gameMode"] = "normal",
                        ["preGeneratedDices1d20"] = scenario.AcceptedState["acceptedDice"]!.DeepClone()
                    }.ToJsonString());
                var effectRoots = CreateEffectRoots(scenario);
                File.WriteAllText(
                    fileSystem.ResolvePath(EffectCarrierCatalog.PlayerPath),
                    effectRoots.PlayerEffects.ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath(EffectIdentityState.StatePath),
                    effectRoots.IdentityIndex.ToJsonString());

                var acceptedEvents = new[]
                {
                    new WoundAcceptedEventAuthority(
                        scenario.EventRef,
                        "mortal_wound_treatment",
                        "t061_accepted_event_authority",
                        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
                };
                var binding = new WoundAcceptedTurnBinding(
                    scenario.AcceptedState["sessionId"]!.GetValue<string>(),
                    scenario.AcceptedState["requestId"]!.GetValue<string>(),
                    scenario.AcceptedState["snapshotToken"]!.GetValue<string>(),
                    "mortal_world",
                    scenario.AcceptedState["turn"]!.GetValue<int>(),
                    acceptedEvents,
                    WoundAcceptedEventSetFingerprint.Compute(acceptedEvents));
                var lease = fileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
                return new AcceptedStateFixture(
                    root,
                    fileSystem,
                    lease,
                    binding,
                    new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["realm"] = "mortal_world",
                        ["targetKind"] = "player",
                        ["targetId"] = "player_current",
                        ["providerKind"] = "npc",
                        ["providerId"] = "field_medic_01",
                        ["currentLocationId"] = "loc_field_clinic_001"
                    },
                    scenario.Before["woundId"]!.GetValue<string>());
            }
            catch
            {
                Directory.Delete(root, recursive: true);
                throw;
            }
        }

        internal object GetAcceptedState()
        {
            var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
                throwOnError: false,
                ignoreCase: false);
            Assert.NotNull(authorityType);
            var parseContext = ExactStaticMethod(authorityType, "ParseContext", 2);
            var parsedContext = Invoke(parseContext, new object?[] { Context.ToJsonString(), "treatmentContext" });
            var context = ReadValidTypedResult(parsedContext, "Context", "T060 context");
            var registry = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.AcceptedTurnAuthorityRegistry",
                throwOnError: false,
                ignoreCase: false);
            Assert.NotNull(registry);
            var getAcceptedState = ExactStaticMethod(
                registry,
                "GetOrBuildMortalWoundTreatmentAcceptedState",
                5);
            var result = Invoke(getAcceptedState, new object?[]
            {
                FileSystem,
                Lease,
                Binding,
                context,
                WoundId
            });
            return ReadValidTypedResult(result, "Authority", "T061 accepted state");
        }

        internal void AssertUnchangedT060RequirementResolution(WoundTreatmentRoute route)
        {
            var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
                throwOnError: false,
                ignoreCase: false);
            Assert.NotNull(authorityType);
            var context = ReadValidTypedResult(
                Invoke(ExactStaticMethod(authorityType, "ParseContext", 2),
                    new object?[] { Context.ToJsonString(), "treatmentContext" }),
                "Context", "T060 context");
            var snapshot = ReadValidTypedResult(
                Invoke(ExactStaticMethod(authorityType, "ParseSnapshot", 2),
                    new object?[] { CreateT060Snapshot().ToJsonString(), "treatmentSnapshot" }),
                "Snapshot", "T060 snapshot");
            var result = Invoke(ExactStaticMethod(authorityType, "ResolveRequirements", 3),
                new[] { (object)route, context, snapshot });
            Assert.True(Assert.IsType<bool>(ReadRequiredProperty(result, "Success")),
                "The canonical procedure fixture must satisfy the unchanged T060 authority.");
        }

        private static JsonObject CreateT060Snapshot() => new()
        {
            ["schemaVersion"] = 1,
            ["snapshotToken"] = "snapshot_t061",
            ["items"] = new JsonArray(new JsonObject
            {
                ["itemId"] = "sterile_thread", ["displayName"] = "Sterile thread",
                ["realm"] = "mortal_world", ["ownerKind"] = "npc", ["ownerId"] = "field_medic_01",
                ["count"] = 2, ["availableCount"] = 2, ["reservationState"] = "available",
                ["lifecycle"] = "active", ["active"] = true
            }),
            ["resources"] = new JsonArray(),
            ["actors"] = new JsonArray(
                new JsonObject
                {
                    ["actorKind"] = "player", ["actorId"] = "player_current", ["displayName"] = "Patient",
                    ["realm"] = "mortal_world", ["currentLocationId"] = "loc_field_clinic_001",
                    ["lifecycle"] = "active", ["active"] = true, ["reachable"] = true,
                    ["skills"] = new JsonArray(new JsonObject
                    {
                        ["capabilityRef"] = "field_medicine", ["displayName"] = "Field Medicine",
                        ["tier"] = 3, ["lifecycle"] = "active", ["active"] = true
                    }),
                    ["capabilities"] = new JsonArray(), ["consents"] = new JsonArray()
                },
                new JsonObject
                {
                    ["actorKind"] = "npc", ["actorId"] = "field_medic_01", ["displayName"] = "Field medic",
                    ["realm"] = "mortal_world", ["currentLocationId"] = "loc_field_clinic_001",
                    ["lifecycle"] = "active", ["active"] = true, ["reachable"] = true,
                    ["skills"] = new JsonArray(new JsonObject
                    {
                        ["capabilityRef"] = "exact_materialized_healing_source", ["displayName"] = "Guaranteed care",
                        ["tier"] = 3, ["lifecycle"] = "active", ["active"] = true
                    }),
                    ["capabilities"] = new JsonArray(), ["consents"] = new JsonArray()
                }),
            ["facilities"] = new JsonArray(), ["locations"] = new JsonArray(), ["quests"] = new JsonArray(),
            ["effects"] = new JsonArray(), ["environments"] = new JsonArray()
        };

        public void Dispose()
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Directory.Delete(Root, recursive: true);
        }
    }
}
