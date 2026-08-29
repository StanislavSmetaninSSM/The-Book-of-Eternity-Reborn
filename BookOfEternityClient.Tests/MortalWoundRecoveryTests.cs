using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// RED boundary for T062/T069. The recovery contract deliberately did not freeze a
/// callable planner signature.  This file therefore fixes the smallest authority-only
/// seam: MortalWoundRecoveryPlanner.Plan(FileSystemManager, CanonicalWriteLease,
/// WoundAcceptedTurnBinding, string woundId).  It must read the canonical wound,
/// history, and world clock itself; callers cannot supply a minute, tick key,
/// fingerprint, result, mutation plan, or deterioration authority.
/// The separate T069 classifier is likewise authority-only:
/// MortalWoundDeteriorationPolicyAuthority.Create(FileSystemManager,
/// CanonicalWriteLease, WoundAcceptedTurnBinding, string woundId, string policyRef).
/// T061's interruption resolver may consume its typed result, but cannot classify
/// a raw policy itself.
/// </summary>
public sealed class MortalWoundRecoveryTests
{
    private const string PlannerTypeName =
        "BookOfEternityClient.Services.MortalWoundRecoveryPlanner";

    public static IEnumerable<object[]> RecoveryPlannerRows => new[]
    {
        RecoveryScenario.ProgressiveDue(),
        RecoveryScenario.RequiresStabilizationBlocked(),
        RecoveryScenario.NoNaturalRecovery(),
        RecoveryScenario.GraceBoundary(),
        RecoveryScenario.ExactReplayPrecedesLiveClock(),
        RecoveryScenario.CheckedMinuteOverflowRejected(),
        RecoveryScenario.DeathContourStopsAtOwningLifecycle()
    }.Select(static scenario => new object[] { scenario });

    public static IEnumerable<object[]> InterruptionPolicyRows => new[]
    {
        RecoveryScenario.StrictlyWorseningInterruption(),
        RecoveryScenario.NeutralInterruptionRejected(),
        RecoveryScenario.BeneficialInterruptionRejected()
    }.Select(static scenario => new object[] { scenario });

    public static IEnumerable<object[]> SemanticRows => RecoveryPlannerRows.Concat(InterruptionPolicyRows);

    [Theory]
    [MemberData(nameof(RecoveryPlannerRows))]
    public void FixtureControl_RecoveryWoundHistoryAndCanonicalWorldMinuteAreCurrentlyValid(
        RecoveryScenario scenario)
    {
        var wound = WoundMaterializationContract.Parse(scenario.Wound.ToJsonString(), "wound");
        var history = WoundHistoryState.Parse(scenario.History.ToJsonString(), "history");
        var worldMinute = EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(
            scenario.WorldTime.ToJsonString());

        Assert.True(wound.IsValid, DescribeIssues(wound.Issues));
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.Equal(scenario.WorldMinute, worldMinute);
        Assert.Equal("world_time.currentTimeInMinutes",
            wound.Wound!.Recovery.ClockKind);
        Assert.Equal(scenario.Mode, wound.Wound.Recovery.Mode);
    }

    [Theory]
    [MemberData(nameof(SemanticRows))]
    public void Plan_UsesOnlyCanonicalRecoveryAuthorityAndReturnsTheExpectedT062Disposition(
        RecoveryScenario scenario)
    {
        using var fixture = RecoveryFixture.Create(scenario);
        fixture.AssertCurrentCanonicalInputs();

        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            PlannerTypeName,
            throwOnError: false,
            ignoreCase: false);
        Assert.True(planner is not null,
            $"T069 recovery planner is absent; '{scenario.Name}' cannot bypass its canonical authority boundary.");

        var plan = ExactStaticMethod(planner!, "Plan", 4);
        Assert.Equal("MortalWoundRecoveryPlanningResult", plan.ReturnType.Name);
        Assert.Equal(typeof(FileSystemManager), plan.GetParameters()[0].ParameterType);
        Assert.Equal(fixture.Lease.GetType(), plan.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(WoundAcceptedTurnBinding), plan.GetParameters()[2].ParameterType);
        Assert.Equal(typeof(string), plan.GetParameters()[3].ParameterType);
        Assert.DoesNotContain(plan.GetParameters(), parameter =>
            parameter.ParameterType == typeof(long) ||
            parameter.ParameterType == typeof(JsonNode) ||
            parameter.Name!.Contains("fingerprint", StringComparison.OrdinalIgnoreCase) ||
            parameter.Name.Contains("tick", StringComparison.OrdinalIgnoreCase) ||
            parameter.Name.Contains("plan", StringComparison.OrdinalIgnoreCase));

        var result = Invoke(plan, new object?[]
        {
            fixture.FileSystem,
            fixture.Lease,
            fixture.Binding,
            fixture.WoundId
        });
        AssertRecoveryResult(result, scenario);
    }

    [Theory]
    [MemberData(nameof(InterruptionPolicyRows))]
    public void InterruptionPolicy_RequiresTypedT069StrictWorseningAuthority(
        RecoveryScenario scenario)
    {
        using var fixture = RecoveryFixture.Create(scenario);
        fixture.AssertCurrentCanonicalInputs();

        var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundDeteriorationPolicyAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(authorityType is not null,
            $"T069 deterioration authority is absent; '{scenario.Name}' must not be classified from raw route JSON.");

        var create = ExactStaticMethod(authorityType!, "Create", 5);
        Assert.Equal("MortalWoundDeteriorationPolicyAuthorityResult", create.ReturnType.Name);
        Assert.Equal(typeof(FileSystemManager), create.GetParameters()[0].ParameterType);
        Assert.Equal(fixture.Lease.GetType(), create.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(WoundAcceptedTurnBinding), create.GetParameters()[2].ParameterType);
        Assert.Equal(typeof(string), create.GetParameters()[3].ParameterType);
        Assert.Equal(typeof(string), create.GetParameters()[4].ParameterType);
        Assert.DoesNotContain(create.GetParameters(), parameter =>
            parameter.ParameterType == typeof(JsonNode) ||
            parameter.Name!.Contains("fingerprint", StringComparison.OrdinalIgnoreCase));

        var result = Invoke(create, new object?[]
        {
            fixture.FileSystem,
            fixture.Lease,
            fixture.Binding,
            fixture.WoundId,
            fixture.PolicyRef
        });
        Assert.Equal(
            new[] { "Authority", "IsValid", "Issues" },
            result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));
        var isValid = Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid"));
        var issues = ReadEnumerableProperty(result, "Issues")
            .Select(Assert.IsType<ValidationIssue>)
            .ToArray();
        var authority = ReadPropertyAllowingNull(result, "Authority");

        Assert.Equal(scenario.ExpectedPolicyAuthority, isValid);
        if (!isValid)
        {
            Assert.NotEmpty(issues);
            Assert.Null(authority);
            return;
        }

        Assert.Empty(issues);
        Assert.NotNull(authority);
        Assert.Equal(fixture.PolicyRef,
            Assert.IsType<string>(ReadRequiredProperty(authority!, "PolicyRef")));
        Assert.Equal("StrictlyWorsening",
            Convert.ToString(ReadRequiredProperty(authority, "Classification")));
        Assert.NotNull(ReadRequiredProperty(authority, "AuthorityFingerprint"));
    }

    private static void AssertRecoveryResult(object result, RecoveryScenario scenario)
    {
        Assert.Equal(
            new[] { "Disposition", "Issues", "ReplayReceipt", "Resolution" },
            result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));

        var disposition = Convert.ToString(ReadRequiredProperty(result, "Disposition"));
        var issues = ReadEnumerableProperty(result, "Issues")
            .Select(Assert.IsType<ValidationIssue>)
            .ToArray();
        var resolution = ReadPropertyAllowingNull(result, "Resolution");
        var replayReceipt = ReadPropertyAllowingNull(result, "ReplayReceipt");

        Assert.Equal(scenario.ExpectedDisposition, disposition);
        if (scenario.ExpectedDisposition == "Rejected")
        {
            Assert.NotEmpty(issues);
            Assert.Null(resolution);
            Assert.Null(replayReceipt);
            return;
        }

        Assert.Empty(issues);
        if (scenario.ExpectedDisposition == "ExactReplay")
        {
            Assert.Null(resolution);
            Assert.NotNull(replayReceipt);
            AssertDetachedReplayReceipt(replayReceipt!);
            return;
        }

        Assert.Null(replayReceipt);
        Assert.NotNull(resolution);
        AssertRecoveryResolution(resolution!, scenario);
    }

    private static void AssertRecoveryResolution(object resolution, RecoveryScenario scenario)
    {
        // The outer four-field shell is deliberately exact.  T069 owns the rest of
        // this new DTO, but these public properties are the minimum observable
        // recovery evidence: no caller-created tick/mutation object can replace it.
        Assert.Equal(scenario.Mode, Assert.IsType<string>(ReadRequiredProperty(resolution, "Mode")));
        Assert.Equal("world_time.currentTimeInMinutes",
            Assert.IsType<string>(ReadRequiredProperty(resolution, "ClockKind")));
        Assert.Equal(EffectAcceptedTurnInputComposer.WorldTimePath,
            Assert.IsType<string>(ReadRequiredProperty(resolution, "ClockSourcePath")));
        Assert.Equal(scenario.WorldMinute,
            Assert.IsType<long>(ReadRequiredProperty(resolution, "CurrentTimeInMinutes")));
        Assert.NotEqual(string.Empty, Assert.IsType<string>(ReadRequiredProperty(resolution, "TickKey")));
        Assert.Equal(scenario.ExpectedRecoveryDisposition,
            Convert.ToString(ReadRequiredProperty(resolution, "RecoveryDisposition")));

        if (scenario.ExpectedPolicyRef is null)
        {
            Assert.Null(ReadPropertyAllowingNull(resolution, "DeteriorationPolicyRef"));
        }
        else
        {
            Assert.Equal(scenario.ExpectedPolicyRef,
                Assert.IsType<string>(ReadRequiredProperty(resolution, "DeteriorationPolicyRef")));
        }

        Assert.Equal(scenario.ExpectedDeathBoundary,
            Convert.ToString(ReadRequiredProperty(resolution, "DeathBoundary")));
        Assert.Equal(scenario.ExpectedHistoryIntent,
            ReadPropertyAllowingNull(resolution, "HistoryIntent") is not null);
    }

    private static void AssertDetachedReplayReceipt(object receipt)
    {
        Assert.False(receipt is JsonNode);
        Assert.NotNull(ReadRequiredProperty(receipt, "TickKey"));
        Assert.NotNull(ReadRequiredProperty(receipt, "ReceiptFingerprint"));
        Assert.Null(ReadPropertyAllowingNull(receipt, "HistoryIntent"));
    }

    private static MethodInfo ExactStaticMethod(Type type, string name, int parameterCount) =>
        Assert.Single(
            type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => candidate.Name == name && candidate.GetParameters().Length == parameterCount);

    private static object Invoke(MethodInfo method, object?[] arguments)
    {
        try
        {
            return method.Invoke(null, arguments)
                ?? throw new InvalidOperationException($"{method.Name} returned null.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static object ReadRequiredProperty(object instance, string name) =>
        ReadPropertyAllowingNull(instance, name)
        ?? throw new Xunit.Sdk.XunitException($"Missing non-null {name}.");

    private static object? ReadPropertyAllowingNull(object instance, string name)
    {
        var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return property!.GetValue(instance);
    }

    private static IEnumerable<object> ReadEnumerableProperty(object instance, string name) =>
        Assert.IsAssignableFrom<IEnumerable>(ReadRequiredProperty(instance, name)).Cast<object>();

    private static string DescribeIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(" | ", issues.Select(issue => $"{issue.Code}@{issue.FilePath}"));

    public sealed record RecoveryScenario(
        string Name,
        string Mode,
        long WorldMinute,
        JsonObject Wound,
        JsonObject History,
        JsonObject WorldTime,
        string ExpectedDisposition,
        string ExpectedRecoveryDisposition,
        string? ExpectedPolicyRef,
        string ExpectedDeathBoundary,
        bool ExpectedHistoryIntent,
        bool ExpectedPolicyAuthority)
    {
        internal static RecoveryScenario ProgressiveDue() => Create(
            "progressive_at_inclusive_cadence_advances_once",
            "progressive",
            worldMinute: 1_440,
            expectedDisposition: "Resolved",
            expectedRecoveryDisposition: "Progressed");

        internal static RecoveryScenario RequiresStabilizationBlocked() => Create(
            "requires_stabilization_blocks_unstabilized_wound",
            "requires_stabilization",
            worldMinute: 1_440,
            expectedDisposition: "Resolved",
            expectedRecoveryDisposition: "BlockedNotStabilized",
            stabilized: false);

        internal static RecoveryScenario NoNaturalRecovery() => Create(
            "no_natural_recovery_has_no_tick_mutation",
            "no_natural_recovery",
            worldMinute: 1_440,
            expectedDisposition: "Resolved",
            expectedRecoveryDisposition: "NoNaturalRecovery");

        internal static RecoveryScenario GraceBoundary() => Create(
            "deterioration_grace_boundary_is_inclusive",
            "requires_stabilization",
            worldMinute: 30,
            policyKind: "increase_severity",
            expectedDisposition: "Resolved",
            expectedRecoveryDisposition: "Deteriorated",
            expectedPolicyRef: "untreated_infection",
            stabilized: false);

        internal static RecoveryScenario StrictlyWorseningInterruption() => Create(
            "course_interruption_uses_strictly_worsening_policy",
            "requires_stabilization",
            worldMinute: 30,
            policyKind: "increase_severity",
            expectedDisposition: "Resolved",
            expectedRecoveryDisposition: "Deteriorated",
            expectedPolicyRef: "missed_course_dose",
            useCourseInterruption: true,
            stabilized: false);

        internal static RecoveryScenario NeutralInterruptionRejected() => Create(
            "course_interruption_rejects_neutral_deterioration_policy",
            "requires_stabilization",
            worldMinute: 30,
            policyKind: "no_change",
            expectedDisposition: "Rejected",
            expectedRecoveryDisposition: "",
            expectedPolicyRef: null,
            useCourseInterruption: true,
            stabilized: false);

        internal static RecoveryScenario BeneficialInterruptionRejected() => Create(
            "course_interruption_rejects_beneficial_deterioration_policy",
            "requires_stabilization",
            worldMinute: 30,
            policyKind: "add_recovery",
            expectedDisposition: "Rejected",
            expectedRecoveryDisposition: "",
            expectedPolicyRef: null,
            useCourseInterruption: true,
            stabilized: false);

        internal static RecoveryScenario ExactReplayPrecedesLiveClock() => Create(
            "exact_recovery_replay_precedes_live_clock_read",
            "progressive",
            worldMinute: 1_440,
            expectedDisposition: "ExactReplay",
            expectedRecoveryDisposition: "",
            replay: true);

        internal static RecoveryScenario CheckedMinuteOverflowRejected() => Create(
            "checked_cadence_or_grace_overflow_rejects_before_tick",
            "requires_stabilization",
            worldMinute: long.MaxValue,
            policyKind: "increase_severity",
            expectedDisposition: "Rejected",
            expectedRecoveryDisposition: "",
            expectedPolicyRef: null,
            cadence: 1L,
            policyGraceMinutes: long.MaxValue,
            stabilized: false);

        internal static RecoveryScenario DeathContourStopsAtOwningLifecycle() => Create(
            "mortal_death_contour_requires_owning_lifecycle_boundary",
            "requires_stabilization",
            worldMinute: 30,
            policyKind: "death_contour",
            expectedDisposition: "Resolved",
            expectedRecoveryDisposition: "DeathBoundaryReached",
            expectedPolicyRef: "untreated_infection",
            deathBoundary: "RequiresOwningLifecycle",
            stabilized: false);

        private static RecoveryScenario Create(
            string name,
            string mode,
            long worldMinute,
            string expectedDisposition,
            string expectedRecoveryDisposition,
            string? policyKind = null,
            string? expectedPolicyRef = null,
            long cadence = 1_440L,
            long policyGraceMinutes = 30L,
            bool useCourseInterruption = false,
            bool stabilized = true,
            bool replay = false,
            string deathBoundary = "None")
        {
            var wound = WoundContractTestData.CreateActiveWound();
            wound["care"]!["state"] = stabilized ? "stabilized" : "untreated";
            wound["care"]!["stabilizedAtTurn"] = stabilized ? 42 : null;
            wound["recovery"] = RecoveryRoot(
                mode,
                cadence,
                policyKind,
                expectedPolicyRef,
                policyGraceMinutes);
            if (useCourseInterruption)
                ConfigureCourseInterruption(wound, expectedPolicyRef ?? "untreated_infection");

            var history = replay
                ? WoundContractTestData.CreateHistory(WoundContractTestData.CreateTransition(
                    kind: "recover",
                    terminal: false))
                : WoundContractTestData.CreateHistory();
            if (replay)
            {
                // This row is canonical persisted evidence from a prior accepted tick;
                // the planner owns key derivation and must return it before reading the
                // contemporaneous clock again.
                var row = history["transitions"]![0]!.AsObject();
                row["cycleKey"] = "recovery_tick_wound_test_torn_side_001";
                row["operationKey"] = "recovery_operation_wound_test_torn_side_001";
            }

            return new RecoveryScenario(
                name,
                mode,
                worldMinute,
                wound,
                history,
                new JsonObject { ["currentTimeInMinutes"] = worldMinute },
                expectedDisposition,
                expectedRecoveryDisposition,
                expectedPolicyRef,
                deathBoundary,
                expectedRecoveryDisposition is "Progressed" or "Deteriorated",
                policyKind is "increase_severity" or "death_contour");
        }
    }

    private static JsonObject RecoveryRoot(
        string mode,
        long cadence,
        string? policyKind,
        string? policyRef,
        long policyGraceMinutes) => new()
    {
        ["mode"] = mode,
        ["clockKind"] = "world_time.currentTimeInMinutes",
        ["cadence"] = cadence,
        ["currentStepProgress"] = 0,
        ["currentStepThreshold"] = 1,
        ["lastTickKey"] = null,
        ["blockers"] = mode == "requires_stabilization"
            ? new JsonArray("not_stabilized")
            : new JsonArray(),
        ["carryOverflow"] = true,
        ["deteriorationPolicy"] = policyKind is null ? null : new JsonObject
        {
            // T069 owns strict parsing of this currently opaque canonical field.
            ["policyRef"] = policyRef ?? "untreated_infection",
            ["unmetConditions"] = new JsonArray("not_stabilized"),
            ["graceMinutes"] = policyGraceMinutes,
            ["cadenceMinutes"] = 10L,
            ["result"] = new JsonObject { ["kind"] = policyKind }
        }
    };

    private static void ConfigureCourseInterruption(JsonObject wound, string policyRef)
    {
        var route = wound["treatment"]!["routes"]![0]!.AsObject();
        route.Clear();
        route["routeId"] = "recovery_course";
        route["displayName"] = "Recovery course";
        route["visibility"] = "known_to_player";
        route["mode"] = "course";
        wound["treatment"]!["knownRouteIds"] = new JsonArray("recovery_course");
        route["requirements"] = new JsonArray(new JsonObject
        {
            ["kind"] = "provider",
            ["providerRef"] = "field_medic_01"
        });
        route["resourcePolicy"] = new JsonObject
        {
            ["reserveBeforeResolution"] = true,
            ["consumeOn"] = new JsonArray("success"),
            ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"),
            ["mutations"] = new JsonArray(new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "course_milestone",
                ["milestoneOrdinal"] = 1,
                ["requirementIndex"] = 0
            })
        };
        route["resolution"] = new JsonObject
        {
            ["clockKind"] = "world_time.currentTimeInMinutes",
            ["maximumGapMinutes"] = 600L
        };
        route["outcomes"] = new JsonArray(
            new JsonObject
            {
                ["ordinal"] = 1,
                ["afterMinutes"] = 0L,
                ["requirements"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "item_quantity",
                    ["itemRef"] = "antibiotic_dose",
                    ["quantity"] = 1,
                    ["ownerRole"] = "target"
                }),
                ["category"] = "success",
                ["completion"] = "completed",
                ["result"] = new JsonArray(new JsonObject { ["kind"] = "stabilize" })
            });
        route["interruption"] = new JsonObject
        {
            ["category"] = "failed_attempt",
            ["result"] = new JsonArray(new JsonObject
            {
                ["kind"] = "apply_deterioration",
                ["policyRef"] = policyRef
            })
        };
    }

    private sealed class RecoveryFixture : IDisposable
    {
        private RecoveryFixture(
            string root,
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease,
            WoundAcceptedTurnBinding binding,
            string woundId,
            RecoveryScenario scenario)
        {
            Root = root;
            FileSystem = fileSystem;
            Lease = lease;
            Binding = binding;
            WoundId = woundId;
            Scenario = scenario;
        }

        private string Root { get; }
        internal FileSystemManager FileSystem { get; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; }
        internal WoundAcceptedTurnBinding Binding { get; }
        internal string WoundId { get; }
        internal string PolicyRef => Assert.IsType<JsonObject>(Scenario.Wound["recovery"]!["deteriorationPolicy"])["policyRef"]!.GetValue<string>();
        private RecoveryScenario Scenario { get; }

        internal static RecoveryFixture Create(RecoveryScenario scenario)
        {
            var root = Path.Combine(Path.GetTempPath(), "boe-t062-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var fileSystem = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fileSystem.EnsureDirectoryStructure();
            try
            {
                Write(fileSystem, "game_state/player/wounds.json",
                    WoundContractTestData.CreatePlayerCarrier(scenario.Wound));
                Write(fileSystem, WoundHistoryState.HistoryPath, scenario.History);
                Write(fileSystem, EffectAcceptedTurnInputComposer.WorldTimePath, scenario.WorldTime);
                Write(fileSystem, "input/turn_request.json", new JsonObject
                {
                    ["sessionId"] = "session_t062",
                    ["requestId"] = "request_t062",
                    ["turnNumber"] = 42,
                    ["gameMode"] = "normal",
                    ["preGeneratedDices1d20"] = new JsonArray(17)
                });

                var events = new[]
                {
                    new WoundAcceptedEventAuthority(
                        "turn_42:recovery_tick",
                        "mortal_wound_recovery",
                        "t062_recovery_event",
                        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
                };
                var binding = new WoundAcceptedTurnBinding(
                    "session_t062",
                    "request_t062",
                    "snapshot_t062",
                    "mortal_world",
                    42,
                    events,
                    WoundAcceptedEventSetFingerprint.Compute(events));
                var lease = fileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
                return new RecoveryFixture(
                    root,
                    fileSystem,
                    lease,
                    binding,
                    scenario.Wound["woundId"]!.GetValue<string>(),
                    scenario);
            }
            catch
            {
                Directory.Delete(root, recursive: true);
                throw;
            }
        }

        internal void AssertCurrentCanonicalInputs()
        {
            var wound = WoundMaterializationContract.Parse(
                File.ReadAllText(FileSystem.ResolvePath("game_state/player/wounds.json")),
                "playerWounds");
            var history = WoundHistoryState.Parse(
                File.ReadAllText(FileSystem.ResolvePath(WoundHistoryState.HistoryPath)),
                "history");
            var worldMinute = EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(
                File.ReadAllText(FileSystem.ResolvePath(EffectAcceptedTurnInputComposer.WorldTimePath)));
            Assert.True(wound.IsValid, DescribeIssues(wound.Issues));
            Assert.True(history.IsValid, DescribeIssues(history.Issues));
            Assert.Equal(Scenario.WorldMinute, worldMinute);
        }

        public void Dispose()
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Directory.Delete(Root, recursive: true);
        }

        private static void Write(FileSystemManager fileSystem, string relativePath, JsonObject root)
        {
            var fullPath = fileSystem.ResolvePath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, root.ToJsonString());
        }
    }
}
