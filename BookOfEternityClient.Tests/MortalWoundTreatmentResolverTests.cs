using System.Reflection;
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
public sealed class MortalWoundTreatmentResolverTests
{
    private const string PlannerTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentPlanner";

    public static IEnumerable<object[]> ProcedureRows => Rows(
        "procedure_normal_contiguous_restart_safe_dice_reservation",
        "procedure_advantage_contiguous_restart_safe_dice_reservation",
        "procedure_disadvantage_contiguous_restart_safe_dice_reservation",
        "procedure_fate_shield_reservation_collision",
        "procedure_numeric_category_and_natural_one_boundary",
        "procedure_natural_twenty_has_applicable_positive_transition",
        "procedure_band_boundary_and_checked_overflow",
        "procedure_all_bands_simulate_before_die_reservation",
        "procedure_requirement_bundle_has_every_scope_and_kind_witness",
        "procedure_typed_failure_witness_preserves_current_observation",
        "procedure_current_applicability_rejects_free_reroll",
        "procedure_combatant_and_member_targeting_is_exact",
        "procedure_same_turn_typed_and_legacy_fate_duplicate_rejects",
        "procedure_critical_reaction_is_typed_and_legacy_compose_stays_compatible",
        "procedure_selected_outcome_and_route_completion_are_deterministic");

    public static IEnumerable<object[]> CourseRows => Rows(
        "course_first_start_creates_complete_start_authority",
        "course_single_active_course_lifecycle",
        "course_current_time_due_boundary_is_inclusive",
        "course_deadline_boundary_is_inclusive",
        "course_satisfied_milestone_has_bound_bundle_and_resource_coordinates",
        "course_unsatisfied_milestone_has_no_reservation",
        "course_invalid_authority_dominates_other_outcomes",
        "course_nonbeneficial_interruption_rejects",
        "course_restart_reconstructs_single_course_identity",
        "course_coordinate_conflict_rejects",
        "course_final_milestone_requires_positive_nonempty_result",
        "course_intermediate_milestone_does_not_complete_route");

    public static IEnumerable<object[]> GuaranteedRows => Rows(
        "guaranteed_uses_current_sealed_capability_proof",
        "guaranteed_revalidates_final_composed_capability_proof",
        "guaranteed_sibling_tier_gate_remains_required",
        "guaranteed_source_is_player_or_npc_not_combatant",
        "guaranteed_heal_at_severity_one_requires_capability_limit",
        "guaranteed_aggregate_reduction_and_legacy_limits_are_checked",
        "guaranteed_outcome_is_ordered_typed_intents_only",
        "guaranteed_complication_ref_rewrites_to_canonical_complication_id",
        "guaranteed_effectful_and_effectless_complication_proposals_reuse_current_contract",
        "guaranteed_derived_ids_and_ref_namespaces_are_deterministic");

    public static IEnumerable<object[]> ReplayAndPersistenceRows => Rows(
        "command_pending_and_history_persist_exact_complete_request_and_bundle",
        "command_pending_byte_semantic_copies_coalesce",
        "nested_request_bundle_and_resource_seals_recompute",
        "probe_exact_replay_returns_detached_request_receipt_before_live_state",
        "probe_invalid_history_dominates_replay_and_conflict",
        "semantic_operation_attempt_and_course_coordinate_conflicts_reject",
        "resolved_payload_is_immutable_and_replay_has_no_intents",
        "one_exact_receipt_and_no_intents_on_reject_or_conflict",
        "attempt_terminal_is_distinct_from_wound_terminal",
        "rollback_and_replay_never_duplicate_intents_or_claims",
        "derived_route_completion_appends_once_only_after_qualifying_success");

    public static IEnumerable<object[]> LegacyRows => Rows(
        "legacy_prepare_finalize_is_two_phase",
        "legacy_cosmetic_draft_has_no_effect_batch",
        "legacy_mechanical_draft_has_one_ordered_effect_batch",
        "legacy_batches_reject_missing_extra_reordered_merged_or_split_rows",
        "legacy_finalize_returns_matching_grouped_existing_effect_results",
        "legacy_child_and_heal_coordinates_are_unique",
        "legacy_wound_survival_is_non_public_and_effect_handoff_is_typed");

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

    [Theory]
    [MemberData(nameof(ReplayAndPersistenceRows))]
    public void Resolver_ReplayAndPersistenceRemainProductionOwned(string scenario) =>
        ExecutePreparedFlow(CreateScenario(scenario, "procedure"));

    [Theory]
    [MemberData(nameof(LegacyRows))]
    public void Resolver_LegacyEffectHandoffRemainsTypedAndOrdered(string scenario) =>
        ExecutePreparedFlow(CreateScenario(scenario, "guaranteed"));

    private static IEnumerable<object[]> Rows(params string[] values) =>
        values.Select(static value => new object[] { value });

    private static ResolverScenario CreateScenario(string name, string mode)
    {
        var before = WoundContractTestData.CreateActiveWound();
        var acceptedState = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["sessionId"] = "session_t061",
            ["requestId"] = "request_t061",
            ["snapshotToken"] = "snapshot_t061",
            ["turn"] = 42,
            ["providerKind"] = "npc",
            ["providerId"] = "npc_field_medic_01",
            ["targetKind"] = "player",
            ["targetId"] = "player_current",
            ["locationId"] = "loc_field_clinic_001",
            ["worldMinute"] = 1_260,
            ["woundId"] = "wound_test_torn_side",
            ["eventRef"] = "turn_42:treatment_event_" + name,
            ["acceptedDice"] = new JsonArray(4, 17, 1, 20),
            ["fateShields"] = new JsonArray("effect_fate_shield_001"),
            ["activeEffectIds"] = new JsonArray("effect_roll_modifier_001"),
            ["skillRows"] = new JsonArray("skill_field_medicine_01"),
            ["canonicalReads"] = new JsonArray(
                "wound_carriers",
                "wound_identity_index",
                "wound_history",
                "world_time",
                "effects",
                "skills")
        };

        // Each row has a distinct production input coordinate.  These are fixture
        // sources only; the planner must read/validate their canonical equivalents
        // itself and must not accept this object as authority.
        acceptedState["operationLabel"] = name;
        acceptedState["requestedMode"] = mode;
        if (name.Contains("restart", StringComparison.Ordinal))
            acceptedState["restartGeneration"] = 2;
        if (name.Contains("combatant", StringComparison.Ordinal))
            acceptedState["targetKind"] = "combatant_member";
        if (name.Contains("severity_one", StringComparison.Ordinal))
        {
            before["severity"]!["value"] = "I";
            before["severity"]!["rank"] = 1;
            before["severity"]!["maximumAtCreation"] = "I";
        }

        return new ResolverScenario(
            name,
            mode,
            acceptedState,
            WoundContractTestData.CreateHistory(),
            before,
            "operation_t061_" + name,
            "clean_and_suture",
            "turn_42:treatment_event_" + name);
    }

    private static void ExecutePreparedFlow(ResolverScenario scenario)
    {
        // Establish concrete scenario inputs before the future production seam.  In
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

        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            PlannerTypeName,
            throwOnError: false,
            ignoreCase: false);

        // This is intentionally the one uniform RED failure for every semantic row:
        // the first absent production-owned seam is the T061 planner.  Once present,
        // the adapter invokes the exact documented factory and corresponding resolver
        // entry; it never substitutes a test-local authority or a hash recipe.
        Assert.True(
            planner is not null,
            $"T061 planner is absent; scenario '{scenario.Name}' cannot enter the production-only accepted-state path.");

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
        var acceptedState = fixture.GetAcceptedState();
        var preparedResult = Invoke(factory, new object?[]
        {
            acceptedState,
            history,
            before.Wound,
            scenario.OperationKey,
            scenario.RouteId,
            scenario.EventRef
        });
        var request = ReadValidTypedResult(preparedResult, "Request", scenario.Name + " request");
        var resolver = ExactStaticMethod(planner, resolverName, 4);
        var resolution = Invoke(resolver, new[]
        {
            request,
            (object?)history,
            before.Wound,
            acceptedState
        });
        AssertResolutionShape(resolution, scenario);
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

    private static void AssertResolutionShape(object result, ResolverScenario scenario)
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
        var expected = scenario.Name.Contains("conflict", StringComparison.Ordinal)
            ? "Conflict"
            : scenario.Name.Contains("replay", StringComparison.Ordinal)
                ? "ExactReplay"
                : scenario.Name.Contains("reject", StringComparison.Ordinal) ||
                  scenario.Name.Contains("invalid", StringComparison.Ordinal) ||
                  scenario.Name.Contains("unsatisfied", StringComparison.Ordinal) ||
                  scenario.Name.Contains("overflow", StringComparison.Ordinal) ||
                  scenario.Name.Contains("inapplicability", StringComparison.Ordinal)
                    ? "Rejected"
                    : "Resolved";
        Assert.Equal(expected, disposition);
        if (expected is "Rejected" or "Conflict")
        {
            Assert.NotEmpty(issues);
            Assert.Null(ReadPropertyAllowingNull(result, "Resolution"));
            Assert.Null(ReadPropertyAllowingNull(result, "ReplayReceipt"));
            return;
        }

        Assert.Empty(issues);
        if (expected == "ExactReplay")
        {
            Assert.Null(ReadPropertyAllowingNull(result, "Resolution"));
            Assert.NotNull(ReadPropertyAllowingNull(result, "ReplayReceipt"));
            return;
        }

        Assert.NotNull(ReadPropertyAllowingNull(result, "Resolution"));
        Assert.Null(ReadPropertyAllowingNull(result, "ReplayReceipt"));
    }

    private static object ReadRequiredProperty(object instance, string name)
    {
        var value = ReadPropertyAllowingNull(instance, name);
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
        string EventRef);

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
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/player/wounds.json"),
                    WoundContractTestData.CreatePlayerCarrier(scenario.Before).ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/wounds/identity_index.json"),
                    WoundContractTestData.CreateIdentityIndex(
                        WoundContractTestData.CreateIdentityEntry()).ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/wounds/history.json"),
                    scenario.History.ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/player/skills_active.json"),
                    new JsonObject { ["activeSkillChanges"] = new JsonArray() }.ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/player/skills_passive.json"),
                    new JsonObject { ["passiveSkillChanges"] = new JsonArray() }.ToJsonString());
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/npcs/npc_core.json"),
                    new JsonObject { ["NPCs"] = new JsonArray() }.ToJsonString());

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
                        ["providerId"] = "npc_field_medic_01",
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

        public void Dispose()
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Directory.Delete(Root, recursive: true);
        }
    }
}
