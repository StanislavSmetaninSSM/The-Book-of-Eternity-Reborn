using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// RED contract for T062/T069 recovery and the T070 persistence handoff. The planner is
/// authority-only: MortalWoundRecoveryPlanner.Plan(FileSystemManager,
/// CanonicalWriteLease, WoundAcceptedTurnBinding, string woundId). It reads carrier,
/// identity, history, anchor and canonical world clock itself; no caller minute, tick,
/// fingerprint, mutation, receipt, history row or policy is accepted.
///
/// The baseline create uses the existing WoundAcceptedTurnPlanner Prepare -> #1535 effect
/// batch -> Finalize pipeline. T070's future
/// AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated overload consumes that sealed
/// <see cref="AcceptedMechanicsWoundStageBundle"/> into the common plan; it is not an
/// alternative create authority. Stabilization is admitted only by T067's sealed
/// treatment request/resolution and T070's six-argument treatment-publication pipeline;
/// this test never manufactures a continuation binding, event, fingerprint, or free
/// stabilization operation.
/// MortalWoundRecoveryAcceptedPlanComposer.Compose(FileSystemManager,
/// CanonicalWriteLease, WoundAcceptedTurnBinding, MortalWoundRecoveryResolution) for a
/// recovery result. Both must register an <see cref="AcceptedMechanicsPlan"/> with the
/// common accepted-plan authority; <see cref="CanonicalStateNormalizer"/> is the sole
/// publisher. Tests never write carrier/index/history, a receipt, or an after-image.
/// T069 interruption classification is separately
/// MortalWoundDeteriorationPolicyAuthority.Create(FileSystemManager, CanonicalWriteLease,
/// WoundAcceptedTurnBinding, string woundId, string policyRef).
/// </summary>
public sealed class MortalWoundRecoveryTests
{
    private const string PlannerName = "BookOfEternityClient.Services.MortalWoundRecoveryPlanner";

    public static IEnumerable<object[]> PlannerRows => new[]
    {
        Scenario.DueMinusOne(), Scenario.Due(), Scenario.DuePlusOne(),
        Scenario.MultiCadenceJump(), Scenario.StabilizationRebasesCadence(),
        Scenario.GraceMinusOne(), Scenario.Grace(), Scenario.GracePlusOne(),
        Scenario.RequiresStabilization(), Scenario.NoNaturalRecovery(),
        Scenario.CheckedOverflow(), Scenario.DeathHandoff(), Scenario.Replay()
    }.Select(static value => new object[] { value });

    public static IEnumerable<object[]> PolicyRows => new[]
    {
        Scenario.StrictlyWorsening(), Scenario.NeutralPolicy(), Scenario.BeneficialPolicy()
    }.Select(static value => new object[] { value });

    [Theory]
    [MemberData(nameof(PlannerRows))]
    public void FixtureControl_RecoveryShapeAndWorldTimeAreCurrentlyValid(Scenario scenario)
    {
        var parsed = WoundMaterializationContract.Parse(scenario.Wound.ToJsonString(), "wound");
        Assert.True(parsed.IsValid, Issues(parsed.Issues));
        Assert.Equal(scenario.CreationMinute,
            EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(scenario.WorldTime.ToJsonString()));
        Assert.Equal("world_time.currentTimeInMinutes", parsed.Wound!.Recovery.ClockKind);
    }

    [Fact]
    public void Authoring_RejectsClientOwnedRecoveryAndDeteriorationAnchors()
    {
        var proposed = WoundContractTestData.CreateActiveWound();
        proposed["recovery"]!["recoveryAnchor"] = new JsonObject
        {
            ["anchorKind"] = "creation", ["anchorMinute"] = 100,
            ["anchorTransitionId"] = "forged_client_anchor"
        };
        proposed["recovery"]!["deteriorationAnchor"] = new JsonObject
        {
            ["conditionKey"] = "not_stabilized", ["anchorMinute"] = 100,
            ["anchorTransitionId"] = "forged_client_anchor"
        };

        var type = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundRecoveryAuthoringAuthority", false, false);
        Assert.True(type is not null,
            "T065/T070 must distinguish a GM proposal from canonical recovery state before accepting anchors.");
        var validate = ExactStatic(type!, "ValidateProposal", 2);
        Assert.Equal("MortalWoundRecoveryAuthoringValidationResult", validate.ReturnType.Name);
        Assert.Equal(typeof(JsonObject), validate.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(string), validate.GetParameters()[1].ParameterType);
        var result = Invoke(validate, proposed, "wound");
        AssertClosed(result, "IsValid", "Issues");
        Assert.False(Assert.IsType<bool>(Required(result, "IsValid")));
        Assert.Contains(Values(result, "Issues").Select(Assert.IsType<ValidationIssue>), issue =>
            issue.Code == "wound_materialization_client_owned_field" &&
            issue.FilePath == "wound.recovery.recoveryAnchor");

        var nullAnchors = WoundContractTestData.CreateActiveWound();
        nullAnchors["recovery"]!["recoveryAnchor"] = null;
        nullAnchors["recovery"]!["deteriorationAnchor"] = null;
        var allowed = Invoke(validate, nullAnchors, "wound");
        AssertClosed(allowed, "IsValid", "Issues");
        Assert.True(Assert.IsType<bool>(Required(allowed, "IsValid")));
        Assert.Empty(Values(allowed, "Issues"));
    }

    [Theory]
    [MemberData(nameof(PlannerRows))]
    public void Plan_UsesCanonicalAnchorClockAndClosedTransitionIntents(Scenario scenario)
    {
        using var fixture = Fixture.Create(scenario);
        fixture.AssertCarrierIdentityHistoryAgreement();

        var resolution = AssertPlannerResult(InvokePlan(fixture), scenario);
        if (!scenario.ReplayAfterCommit)
            return;

        var receipt = ComposeAndPublishRecovery(fixture, resolution!);
        fixture.AssertCarrierIdentityHistoryAgreement();
        fixture.RestartForReplay();
        fixture.AssertCarrierIdentityHistoryAgreement();
        fixture.CorruptLiveClock();
        AssertExactReplay(InvokePlan(fixture), receipt);
    }

    [Theory]
    [MemberData(nameof(PolicyRows))]
    public void InterruptionPolicy_UsesOnlyTypedT069StrictWorseningAuthority(Scenario scenario)
    {
        using var fixture = Fixture.Create(scenario);
        fixture.AssertCarrierIdentityHistoryAgreement();
        var type = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundDeteriorationPolicyAuthority", false, false);
        Assert.True(type is not null, "T069 policy authority is absent; raw policy JSON is not a resolver seam.");
        var method = ExactStatic(type!, "Create", 5);
        Assert.Equal("MortalWoundDeteriorationPolicyAuthorityResult", method.ReturnType.Name);
        Assert.Equal(typeof(FileSystemManager), method.GetParameters()[0].ParameterType);
        Assert.Equal(fixture.Lease.GetType(), method.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(WoundAcceptedTurnBinding), method.GetParameters()[2].ParameterType);
        Assert.Equal(typeof(string), method.GetParameters()[3].ParameterType);
        Assert.Equal(typeof(string), method.GetParameters()[4].ParameterType);
        Assert.DoesNotContain(method.GetParameters(), static p =>
            p.ParameterType == typeof(JsonNode) || p.Name!.Contains("fingerprint", StringComparison.OrdinalIgnoreCase));

        var result = Invoke(method, fixture.FileSystem, fixture.Lease, fixture.Binding, fixture.WoundId, fixture.PolicyRef);
        AssertClosed(result, "Authority", "IsValid", "Issues");
        var valid = Assert.IsType<bool>(Required(result, "IsValid"));
        var issues = Values(result, "Issues").Select(Assert.IsType<ValidationIssue>).ToArray();
        var authority = Optional(result, "Authority");
        Assert.Equal(scenario.PolicyValid, valid);
        if (!valid)
        {
            Assert.Single(issues);
            Assert.Equal("mortal_wound_deterioration_policy_not_strictly_worsening", issues[0].Code);
            Assert.Equal(Fixture.CanonicalDeteriorationPolicyPath, issues[0].FilePath);
            Assert.Null(authority);
            return;
        }

        Assert.Empty(issues);
        Assert.NotNull(authority);
        Assert.Equal(fixture.PolicyRef, Assert.IsType<string>(Required(authority!, "PolicyRef")));
        Assert.Equal("StrictlyWorsening", Convert.ToString(Required(authority, "Classification")));
        AssertFingerprint(Required(authority, "AuthorityFingerprint"));
    }

    [Fact]
    public void InterruptionPolicy_ResolverOverloadBindsAcceptedStateAndAttemptCoordinates()
    {
        var type = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundDeteriorationPolicyAuthority", false, false);
        Assert.True(type is not null, "T069 deterioration authority is absent.");
        var method = ExactStatic(type!, "Create", 3);
        Assert.Equal("MortalWoundDeteriorationPolicyAuthorityResult", method.ReturnType.Name);
        Assert.Equal("MortalWoundTreatmentAcceptedStateAuthority", method.GetParameters()[0].ParameterType.Name);
        Assert.Equal("MortalWoundTreatmentAttemptCoordinates", method.GetParameters()[1].ParameterType.Name);
        Assert.Equal(typeof(string), method.GetParameters()[2].ParameterType);
        Assert.DoesNotContain(method.GetParameters(), static parameter =>
            parameter.ParameterType == typeof(JsonNode) ||
            parameter.Name!.Contains("fingerprint", StringComparison.OrdinalIgnoreCase) ||
            parameter.Name.Contains("wound", StringComparison.OrdinalIgnoreCase));
    }

    private static object InvokePlan(Fixture fixture)
    {
        var type = typeof(WoundMaterializationContract).Assembly.GetType(PlannerName, false, false);
        Assert.True(type is not null, $"T069 planner is absent for '{fixture.Name}'.");
        var method = ExactStatic(type!, "Plan", 4);
        Assert.Equal("MortalWoundRecoveryPlanningResult", method.ReturnType.Name);
        Assert.Equal(typeof(FileSystemManager), method.GetParameters()[0].ParameterType);
        Assert.Equal(fixture.Lease.GetType(), method.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(WoundAcceptedTurnBinding), method.GetParameters()[2].ParameterType);
        Assert.Equal(typeof(string), method.GetParameters()[3].ParameterType);
        Assert.DoesNotContain(method.GetParameters(), static p =>
            p.ParameterType == typeof(long) || p.ParameterType == typeof(JsonNode) ||
            p.Name!.Contains("fingerprint", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("tick", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("plan", StringComparison.OrdinalIgnoreCase));
        return Invoke(method, fixture.FileSystem, fixture.Lease, fixture.Binding, fixture.WoundId);
    }

    private static object? AssertPlannerResult(object result, Scenario scenario)
    {
        AssertClosed(result, "Disposition", "Issues", "ReplayReceipt", "Resolution");
        Assert.Equal(scenario.Disposition, Convert.ToString(Required(result, "Disposition")));
        var issues = Values(result, "Issues").Select(Assert.IsType<ValidationIssue>).ToArray();
        var resolution = Optional(result, "Resolution");
        Assert.Null(Optional(result, "ReplayReceipt"));
        if (scenario.Disposition == "Rejected")
        {
            Assert.Collection(issues, issue =>
            {
                Assert.Equal("mortal_wound_recovery_checked_time_overflow", issue.Code);
                Assert.Equal(EffectAcceptedTurnInputComposer.WorldTimePath, issue.FilePath);
            });
            Assert.Null(resolution);
            return null;
        }

        Assert.Empty(issues);
        Assert.NotNull(resolution);
        AssertResolution(resolution!, scenario);
        return resolution;
    }

    private static void AssertResolution(object resolution, Scenario scenario)
    {
        AssertClosed(resolution,
            "AuthorityFingerprint", "CadenceDueMinute", "ClockKind", "ClockSourcePath",
            "CurrentTimeInMinutes", "DeathHandoff", "DeteriorationGraceDeadlineMinute",
            "DeteriorationAnchorMinute", "DeteriorationPolicyRef", "ElapsedCadences",
            "ElapsedDeteriorationCadences", "Mode", "NextRecoveryAnchorMinute",
            "NextDeteriorationAnchorMinute", "RecoveryAnchorMinute", "RecoveryDisposition",
            "TickKey", "TransitionIntents");
        Assert.Equal(scenario.Mode, Assert.IsType<string>(Required(resolution, "Mode")));
        Assert.Equal("world_time.currentTimeInMinutes", Assert.IsType<string>(Required(resolution, "ClockKind")));
        Assert.Equal(EffectAcceptedTurnInputComposer.WorldTimePath,
            Assert.IsType<string>(Required(resolution, "ClockSourcePath")));
        Assert.Equal(scenario.Minute, Assert.IsType<long>(Required(resolution, "CurrentTimeInMinutes")));
        Assert.Equal(scenario.Anchor, Assert.IsType<long>(Required(resolution, "RecoveryAnchorMinute")));
        Assert.Equal(scenario.DeteriorationAnchor, NullableLong(resolution, "DeteriorationAnchorMinute"));
        Assert.Equal(scenario.Due, NullableLong(resolution, "CadenceDueMinute"));
        Assert.Equal(scenario.GraceDeadline, NullableLong(resolution, "DeteriorationGraceDeadlineMinute"));
        Assert.Equal(scenario.ElapsedCadences, Assert.IsType<long>(Required(resolution, "ElapsedCadences")));
        Assert.Equal(scenario.ElapsedDeteriorationCadences,
            Assert.IsType<long>(Required(resolution, "ElapsedDeteriorationCadences")));
        Assert.Equal(scenario.NextRecoveryAnchor, NullableLong(resolution, "NextRecoveryAnchorMinute"));
        Assert.Equal(scenario.NextDeteriorationAnchor,
            NullableLong(resolution, "NextDeteriorationAnchorMinute"));
        Assert.Equal(scenario.RecoveryDisposition, Convert.ToString(Required(resolution, "RecoveryDisposition")));
        Assert.Equal(scenario.PolicyRefExpected, NullableString(resolution, "DeteriorationPolicyRef"));
        AssertFingerprint(Required(resolution, "AuthorityFingerprint"));

        var intents = Values(resolution, "TransitionIntents").ToArray();
        Assert.Equal(scenario.IntentTypes, intents.Select(static value => value.GetType().Name));
        Assert.All(intents, AssertTypedIntent);
        Assert.DoesNotContain(resolution.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public),
            static p => p.Name.Contains("History", StringComparison.Ordinal) ||
                        p.Name.Contains("WoundMutation", StringComparison.Ordinal) ||
                        p.Name.Contains("DeathMutation", StringComparison.Ordinal));
        var death = Optional(resolution, "DeathHandoff");
        if (scenario.DeathHandoff)
        {
            Assert.NotNull(death);
            Assert.Equal("MortalWoundDeathHandoffIntent", death!.GetType().Name);
            Assert.Contains(intents, value => ReferenceEquals(value, death));
        }
        else
            Assert.Null(death);
    }

    private static void AssertTypedIntent(object value)
    {
        Assert.False(value is JsonNode);
        Assert.Contains(value.GetType().Name, new[]
        {
            "MortalWoundRecoveryProgressIntent", "MortalWoundRecoveryDeteriorationIntent",
            "MortalWoundDeathHandoffIntent"
        });
        AssertClosedIntent(value);
        Assert.DoesNotContain(value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public),
            static p => p.Name.Contains("Json", StringComparison.Ordinal) ||
                        p.Name.Contains("History", StringComparison.Ordinal) ||
                        p.Name.Contains("After", StringComparison.Ordinal) ||
                        p.Name.Contains("Mutation", StringComparison.Ordinal) ||
                        p.Name.Contains("Writer", StringComparison.Ordinal));
    }

    private static void AssertClosedIntent(object value)
    {
        var expected = value.GetType().Name switch
        {
            "MortalWoundRecoveryProgressIntent" => new[]
            {
                "AuthorityFingerprint", "ElapsedCadences", "NextRecoveryAnchorMinute",
                "RecoveryAnchorMinute", "TickKey", "WoundId"
            },
            "MortalWoundRecoveryDeteriorationIntent" => new[]
            {
                "AuthorityFingerprint", "ElapsedCadences", "NextDeteriorationAnchorMinute",
                "PolicyRef", "TickKey", "WoundId"
            },
            "MortalWoundDeathHandoffIntent" => new[]
            {
                "AuthorityFingerprint", "PolicyRef", "TickKey", "WoundId"
            },
            _ => throw new Xunit.Sdk.XunitException($"Unknown recovery intent '{value.GetType().Name}'.")
        };
        AssertClosed(value, expected);
    }

    private static object ComposeAndPublishRecovery(Fixture fixture, object resolution)
    {
        var type = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundRecoveryAcceptedPlanComposer", false, false);
        Assert.True(type is not null,
            "T070 recovery accepted-plan composer is absent; a test must not apply a resolution.");
        var method = ExactStatic(type!, "Compose", 4);
        Assert.Equal("MortalWoundRecoveryAcceptedPlanCompositionResult", method.ReturnType.Name);
        Assert.Equal(typeof(FileSystemManager), method.GetParameters()[0].ParameterType);
        Assert.Equal(fixture.Lease.GetType(), method.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(WoundAcceptedTurnBinding), method.GetParameters()[2].ParameterType);
        Assert.Equal(resolution.GetType(), method.GetParameters()[3].ParameterType);
        var result = Invoke(method, fixture.FileSystem, fixture.Lease, fixture.Binding, resolution);
        AssertClosed(result, "AcceptedPlan", "Disposition", "Issues", "Receipt", "WoundStageBundle");
        Assert.Equal("Composed", Convert.ToString(Required(result, "Disposition")));
        Assert.Empty(Values(result, "Issues"));
        var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(Required(result, "WoundStageBundle"));
        var acceptedPlan = Assert.IsType<AcceptedMechanicsPlan>(Required(result, "AcceptedPlan"));
        AssertPublishedWoundPlan(acceptedPlan, bundle);
        var receipt = Required(result, "Receipt");
        AssertReceipt(receipt);
        Assert.Same(acceptedPlan, PublishAcceptedPlan(fixture));
        return receipt;
    }

    private static object PublishAcceptedPlan(Fixture fixture) =>
        new CanonicalStateNormalizer(fixture.FileSystem, NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(fixture.Lease)
            .NormalizeAcceptedMechanicsAsync(backups: null)
            .GetAwaiter().GetResult()
            ?? throw new Xunit.Sdk.XunitException("Common accepted-plan publisher returned no plan.");

    private static void AssertExactReplay(object result, object receipt)
    {
        AssertClosed(result, "Disposition", "Issues", "ReplayReceipt", "Resolution");
        Assert.Equal("ExactReplay", Convert.ToString(Required(result, "Disposition")));
        Assert.Empty(Values(result, "Issues"));
        Assert.Null(Optional(result, "Resolution"));
        var replay = Required(result, "ReplayReceipt");
        AssertReceipt(replay);
        AssertReceiptEqual(receipt, replay);
    }

    private static void AssertReceipt(object receipt)
    {
        Assert.False(receipt is JsonNode);
        AssertClosed(receipt, "ReceiptFingerprint", "TickKey", "WoundId");
        Assert.NotEqual(string.Empty, Assert.IsType<string>(Required(receipt, "TickKey")));
        AssertFingerprint(Required(receipt, "ReceiptFingerprint"));
    }

    private static void AssertReceiptEqual(object expected, object actual)
    {
        AssertReceipt(expected);
        AssertReceipt(actual);
        foreach (var property in expected.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            Assert.Equal(property.GetValue(expected), Required(actual, property.Name));
    }

    private static void AssertPublishedWoundPlan(
        AcceptedMechanicsPlan plan,
        AcceptedMechanicsWoundStageBundle? expectedBundle)
    {
        var actual = Assert.IsType<AcceptedMechanicsWoundStageBundle>(plan.WoundStageBundle);
        if (expectedBundle is not null)
        {
            Assert.Equal(expectedBundle.InputFingerprint, actual.InputFingerprint);
            Assert.Equal(expectedBundle.WoundPreparationFingerprint, actual.WoundPreparationFingerprint);
            Assert.Equal(expectedBundle.EffectInputFingerprint, actual.EffectInputFingerprint);
            Assert.Equal(expectedBundle.EffectAcceptedTurnPlanFingerprint, actual.EffectAcceptedTurnPlanFingerprint);
            Assert.Equal(expectedBundle.WoundFinalPlanFingerprint, actual.WoundFinalPlanFingerprint);
            Assert.Equal(expectedBundle.BundleFingerprint, actual.BundleFingerprint);
        }
        Assert.Contains(WoundCarrierCatalog.PlayerPath, plan.WoundCarrierAfterImages.Keys);
        Assert.NotEmpty(Assert.IsType<JsonObject>(plan.WoundCarrierAfterImages[WoundCarrierCatalog.PlayerPath]));
        Assert.NotEmpty(Assert.IsType<JsonObject>(plan.WoundIdentityAfterImage));
        Assert.NotEmpty(Assert.IsType<JsonObject>(plan.WoundHistoryAfterImage));
    }

    private static MethodInfo ExactStatic(Type type, string name, int arity) => Assert.Single(
        type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
        method => method.Name == name && method.GetParameters().Length == arity);

    private static object Invoke(MethodInfo method, params object?[] values)
    {
        try { return method.Invoke(null, values) ?? throw new InvalidOperationException($"{method.Name} returned null."); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
    }

    private static void AssertClosed(object instance, params string[] names) => Assert.Equal(
        names.OrderBy(static name => name, StringComparer.Ordinal),
        instance.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetIndexParameters().Length == 0)
            .Select(static property => property.Name).OrderBy(static name => name, StringComparer.Ordinal));

    private static object Required(object value, string property) => Optional(value, property)
        ?? throw new Xunit.Sdk.XunitException($"Missing non-null {property}.");

    private static object? Optional(object value, string property)
    {
        var info = value.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(info);
        return info!.GetValue(value);
    }

    private static IEnumerable<object> Values(object value, string property) =>
        Assert.IsAssignableFrom<IEnumerable>(Required(value, property)).Cast<object>();

    private static string? NullableString(object value, string property) => Optional(value, property) is { } raw
        ? Assert.IsType<string>(raw) : null;

    private static long? NullableLong(object value, string property) => Optional(value, property) is { } raw
        ? Assert.IsType<long>(raw) : null;

    private static void AssertFingerprint(object value) => Assert.True(
        ResourceMaterializationContract.IsAuthorityFingerprint(Assert.IsType<string>(value)));

    private static string Issues(IEnumerable<ValidationIssue> values) =>
        string.Join(" | ", values.Select(static issue => $"{issue.Code}@{issue.FilePath}"));

    public sealed record Scenario(
        string Name, string Mode, long Anchor, long? DeteriorationAnchor, long CreationMinute,
        long? StabilizationMinute, long Minute, JsonObject Wound, JsonObject WorldTime,
        string Disposition, string RecoveryDisposition, string? PolicyRefExpected, long? Due,
        long? GraceDeadline, long ElapsedCadences, long ElapsedDeteriorationCadences,
        long? NextRecoveryAnchor, long? NextDeteriorationAnchor, string[] IntentTypes,
        bool DeathHandoff, bool ReplayAfterCommit,
        bool PolicyValid, bool StartsStabilized)
    {
        internal static Scenario DueMinusOne() => Create("cadence_due_minus_one", "progressive", 109,
            "NotDue", null, 110, null, [], elapsedCadences: 0);
        internal static Scenario Due() => Create("cadence_due", "progressive", 110,
            "Progressed", null, 110, null, ["MortalWoundRecoveryProgressIntent"]);
        internal static Scenario DuePlusOne() => Create("cadence_due_plus_one", "progressive", 111,
            "Progressed", null, 110, null, ["MortalWoundRecoveryProgressIntent"]);
        internal static Scenario MultiCadenceJump() => Create("cadence_jump_three_elapsed", "progressive", 135,
            "Progressed", null, 110, null, ["MortalWoundRecoveryProgressIntent"], elapsedCadences: 3,
            nextRecoveryAnchor: 140);
        internal static Scenario StabilizationRebasesCadence() => Create("stabilization_rebases_cadence", "requires_stabilization", 159,
            "NotDue", null, 160, null, [], stabilizationMinute: 150, anchor: 150);
        internal static Scenario GraceMinusOne() => Create("grace_minus_one", "requires_stabilization", 129,
            "BlockedNotStabilized", "untreated_infection", 110, 130, [], "increase_severity", stabilized: false,
            elapsedCadences: 0);
        internal static Scenario Grace() => Create("grace", "requires_stabilization", 130,
            "Deteriorated", "untreated_infection", 110, 130, ["MortalWoundRecoveryDeteriorationIntent"], "increase_severity", stabilized: false,
            elapsedCadences: 0, elapsedDeteriorationCadences: 1);
        internal static Scenario GracePlusOne() => Create("grace_plus_one", "requires_stabilization", 131,
            "Deteriorated", "untreated_infection", 110, 130, ["MortalWoundRecoveryDeteriorationIntent"], "increase_severity", stabilized: false,
            elapsedCadences: 0, elapsedDeteriorationCadences: 1);
        internal static Scenario RequiresStabilization() => Create("requires_stabilization", "requires_stabilization", 110,
            "BlockedNotStabilized", null, 110, null, [], stabilized: false, elapsedCadences: 0);
        internal static Scenario NoNaturalRecovery() => Create("no_natural_recovery", "no_natural_recovery", 110,
            "NoNaturalRecovery", null, null, null, [], elapsedCadences: 0, nextRecoveryAnchor: null);
        internal static Scenario CheckedOverflow() => Create("checked_anchor_cadence_overflow", "progressive", long.MaxValue,
            "", null, null, null, [], expected: "Rejected", anchor: long.MaxValue - 5, cadence: 10,
            elapsedCadences: 0, creationMinute: long.MaxValue - 5);
        internal static Scenario DeathHandoff() => Create("death_is_lifecycle_handoff", "requires_stabilization", 130,
            "DeathHandoffRequired", "untreated_infection", 110, 130, ["MortalWoundDeathHandoffIntent"], "death_contour", stabilized: false,
            death: true, elapsedCadences: 0, elapsedDeteriorationCadences: 1);
        internal static Scenario Replay() => Create("replay_precedes_malformed_clock", "progressive", 110,
            "Progressed", null, 110, null, ["MortalWoundRecoveryProgressIntent"], replay: true);
        internal static Scenario StrictlyWorsening() => Create("strictly_worsening_interruption", "requires_stabilization", 130,
            "Deteriorated", "missed_course_dose", 110, 130, ["MortalWoundRecoveryDeteriorationIntent"],
            "increase_severity", "missed_course_dose", false, policyValid: true);
        internal static Scenario NeutralPolicy() => Create("neutral_interruption_rejected", "requires_stabilization", 130,
            "", null, 110, 130, [], "no_change", stabilized: false, expected: "Rejected");
        internal static Scenario BeneficialPolicy() => Create("beneficial_interruption_rejected", "requires_stabilization", 130,
            "", null, 110, 130, [], "add_recovery", stabilized: false, expected: "Rejected");

        private static Scenario Create(string name, string mode, long minute, string recoveryDisposition,
            string? expectedPolicyRef, long? due, long? grace, string[] intents, string? policyKind = null,
            string? policyRef = null, bool stabilized = true, string expected = "Resolved", long anchor = 100,
            long cadence = 10, bool death = false, bool replay = false, bool policyValid = false,
            long? stabilizationMinute = null, long? deteriorationAnchor = null, long elapsedCadences = 1,
            long elapsedDeteriorationCadences = 0, long? nextRecoveryAnchor = null,
            long? nextDeteriorationAnchor = null, long creationMinute = 100)
        {
            var wound = WoundContractTestData.CreateActiveWound();
            // GM data may declare recovery mode and cadence, but not a canonical state
            // transition. The accepted creation is always untreated; when a row needs a
            // stabilized wound, Fixture materializes its second transition through T070.
            wound["care"]!["state"] = "untreated";
            wound["care"]!["stabilizedAtTurn"] = null;
            wound["recovery"] = Recovery(mode, cadence, policyKind, policyRef);
            var conditionAnchor = deteriorationAnchor ?? (policyKind is null ? null : creationMinute);
            var elapsedDeterioration = policyKind is null ? 0 : elapsedDeteriorationCadences;
            return new(name, mode, anchor, conditionAnchor, creationMinute, stabilizationMinute, minute, wound,
                new JsonObject { ["currentTimeInMinutes"] = creationMinute }, expected, recoveryDisposition,
                expectedPolicyRef, due, grace, elapsedCadences, elapsedDeterioration,
                nextRecoveryAnchor ?? (expected == "Rejected" ? null :
                    (elapsedCadences == 0 ? anchor + cadence : anchor + (elapsedCadences + 1) * cadence)),
                nextDeteriorationAnchor ?? (conditionAnchor is null || elapsedDeterioration == 0
                    ? (conditionAnchor is null ? null : conditionAnchor + (grace ?? 0))
                    : conditionAnchor + (grace ?? 0) + elapsedDeterioration * cadence),
                intents, death, replay, policyValid, stabilized);
        }
    }

    private static JsonObject Recovery(string mode, long cadence, string? policyKind, string? policyRef) => new()
    {
        ["mode"] = mode, ["clockKind"] = "world_time.currentTimeInMinutes",
        ["cadence"] = cadence, ["currentStepProgress"] = 0, ["currentStepThreshold"] = 1,
        ["lastTickKey"] = null,
        ["blockers"] = mode == "requires_stabilization" ? new JsonArray("not_stabilized") : new JsonArray(),
        ["carryOverflow"] = true,
        ["deteriorationPolicy"] = policyKind is null ? null : new JsonObject
        {
            ["policyRef"] = policyRef ?? "untreated_infection", ["unmetConditions"] = new JsonArray("not_stabilized"),
            ["graceMinutes"] = 30L, ["cadenceMinutes"] = 10L,
            ["result"] = new JsonObject { ["kind"] = policyKind }
        }
    };

    private sealed class Fixture : IDisposable
    {
        private Fixture(string root, FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
            WoundAcceptedTurnBinding binding, string woundId, Scenario scenario)
        { Root = root; FileSystem = fs; Lease = lease; Binding = binding; WoundId = woundId; Scenario = scenario; }

        private string Root { get; }
        internal FileSystemManager FileSystem { get; private set; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; private set; }
        internal WoundAcceptedTurnBinding Binding { get; }
        internal string WoundId { get; }
        internal string Name => Scenario.Name;
        internal string PolicyRef => Assert.IsType<JsonObject>(Scenario.Wound["recovery"]!["deteriorationPolicy"])["policyRef"]!.GetValue<string>();
        private Scenario Scenario { get; }

        internal static Fixture Create(Scenario scenario)
        {
            var root = Path.Combine(Path.GetTempPath(), "boe-t062-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            FileSystemManager.CanonicalWriteLease? lease = null;
            try
            {
                var parsed = WoundMaterializationContract.Parse(scenario.Wound.ToJsonString(), "recoveryBaseline");
                Assert.True(parsed.IsValid, Issues(parsed.Issues));
                var creationInput = WoundEffectBatchPlannerTests.CreateInputForAcceptedCache();
                Write(fs, EffectAcceptedTurnInputComposer.WorldTimePath, scenario.WorldTime);
                WriteTurnRequest(fs, creationInput.Binding);
                AssertLiveTurnCorrelation(fs, creationInput.Binding, creationInput);
                lease = fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
                var creationDraft = Assert.Single(creationInput.Transitions);
                creationInput = creationInput with
                {
                    Transitions = new[] { creationDraft with
                    {
                        ProposedAfter = creationDraft.ProposedAfter with { Recovery = parsed.Wound!.Recovery }
                    }}
                };
                var preparedResult = WoundAcceptedTurnPlanner.Prepare(creationInput);
                Assert.Empty(preparedResult.Issues);
                var prepared = Assert.IsType<WoundPreparedAcceptedTurnPlan>(preparedResult.Plan);
                var effect = WoundEffectBatchPlanner.Build(prepared,
                    WoundEffectBatchPlannerTests.CreateEffectInputForAcceptedCache(prepared),
                    new EffectIdentityFactory());
                Assert.True(effect.Success, Issues(effect.Issues));
                var finalizedResult = WoundAcceptedTurnPlanner.Finalize(prepared, effect);
                Assert.Empty(finalizedResult.Issues);
                var finalized = Assert.IsType<WoundAcceptedTurnPlan>(finalizedResult.Plan);
                var creationBundle = new AcceptedMechanicsWoundStageBundle(
                    creationInput, prepared, Assert.IsType<WoundEffectBatchAcceptedPlan>(effect.Plan), finalized);
                ComposeAndPublishWoundStages(fs, lease, creationBundle);
                var woundId = Assert.Single(finalized.AllocatedWoundIds);
                if (scenario.StartsStabilized)
                {
                    // T067/T070 is the only legal stabilization path.  Keep this RED at
                    // the typed treatment authority rather than inventing a binding or
                    // operation that could grant free stabilization.
                    if (scenario.StabilizationMinute is { } stabilizationMinute)
                        Write(fs, EffectAcceptedTurnInputComposer.WorldTimePath,
                            new JsonObject { ["currentTimeInMinutes"] = stabilizationMinute });
                    lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    lease = null;
                    PrepareLiveTurn(fs, 43, "stabilization");
                    lease = fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
                    ResolveAndPublishStabilization(fs, lease, woundId);
                }
                Write(fs, EffectAcceptedTurnInputComposer.WorldTimePath,
                    new JsonObject { ["currentTimeInMinutes"] = scenario.Minute });
                lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
                lease = null;
                PrepareLiveTurn(fs, 44, "recovery");
                lease = fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
                // T066 must export the recovery binding from the current prepared turn;
                // the bootstrap creation binding is never reused for this continuation.
                var recoveryBinding = ExportRecoveryBinding(fs, lease, woundId);
                return new(root, fs, lease,
                    recoveryBinding,
                    woundId, scenario);
            }
            catch
            {
                if (lease is not null) lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
                Directory.Delete(root, recursive: true);
                throw;
            }
        }

        internal void AssertCarrierIdentityHistoryAgreement()
        {
            var player = Read(WoundCarrierCatalog.PlayerPath);
            var catalog = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(player, null, null, null, null));
            Assert.Empty(catalog.Issues);
            Assert.True(catalog.TryResolveOne(WoundId, out var occurrence));
            var identity = WoundIdentityState.Parse(File.ReadAllText(FileSystem.ResolvePath(WoundIdentityState.StatePath)), WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(File.ReadAllText(FileSystem.ResolvePath(WoundHistoryState.HistoryPath)), WoundHistoryState.HistoryPath);
            Assert.True(identity.IsValid, Issues(identity.Issues));
            Assert.True(history.IsValid, Issues(history.Issues));
            Assert.True(identity.State!.TryGetEntry(WoundId, out var entry));
            Assert.Empty(WoundIdentityState.ValidateActiveAgreement(entry!, occurrence.Wound, "recoveryBaseline"));
            Assert.Empty(history.State!.ValidateAgreement(identity.State, catalog));
        }

        internal void CorruptLiveClock() => Write(FileSystem, EffectAcceptedTurnInputComposer.WorldTimePath,
            new JsonObject { ["currentTimeInMinutes"] = "malformed" });

        internal void RestartForReplay()
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            FileSystem = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance);
            FileSystem.EnsureDirectoryStructure();
            Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
            AssertCarrierIdentityHistoryAgreement();
        }

        public void Dispose() { Lease.DisposeAsync().AsTask().GetAwaiter().GetResult(); Directory.Delete(Root, recursive: true); }

        private static void ComposeAndPublishWoundStages(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsWoundStageBundle bundle)
        {
            var method = ExactStatic(typeof(AcceptedMechanicsPlanAuthority), "GetOrBuildWoundValidated", 3);
            Assert.Equal(typeof(AcceptedMechanicsPlanningResult), method.ReturnType);
            Assert.Equal(typeof(FileSystemManager), method.GetParameters()[0].ParameterType);
            Assert.Equal(lease.GetType(), method.GetParameters()[1].ParameterType);
            Assert.Equal(typeof(AcceptedMechanicsWoundStageBundle), method.GetParameters()[2].ParameterType);
            var result = Invoke(method, fs, lease, bundle);
            AssertClosed(result, "Issues", "Plan", "Success");
            Assert.True(Assert.IsType<bool>(Required(result, "Success")));
            Assert.Empty(Values(result, "Issues"));
            var plan = Assert.IsType<AcceptedMechanicsPlan>(Required(result, "Plan"));
            AssertPublishedWoundPlan(plan, bundle);
            var published = new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease)
                .NormalizeAcceptedMechanicsAsync(backups: null)
                .GetAwaiter().GetResult();
            Assert.Same(plan, published);
        }

        private static void ResolveAndPublishStabilization(
            FileSystemManager fs,
            FileSystemManager.CanonicalWriteLease lease,
            string woundId)
        {
            var planner = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.WoundAcceptedTurnPlanner", false, false);
            Assert.True(planner is not null, "T070 treatment publication is absent.");
            var compose = ExactStatic(planner!, "ComposeMortalWoundTreatmentPublication", 6);
            Assert.Equal(typeof(FileSystemManager), compose.GetParameters()[0].ParameterType);
            Assert.Equal(lease.GetType(), compose.GetParameters()[1].ParameterType);
            Assert.Equal(typeof(GameResponse), compose.GetParameters()[2].ParameterType);
            Assert.Equal("MortalWoundTreatmentAcceptedStateAuthority", compose.GetParameters()[3].ParameterType.Name);
            Assert.Contains("Request", compose.GetParameters()[4].ParameterType.Name, StringComparison.Ordinal);
            Assert.Contains("Resolution", compose.GetParameters()[5].ParameterType.Name, StringComparison.Ordinal);
            var acceptedState = ExportAcceptedState(fs, lease, woundId);
            var binding = Assert.IsType<WoundAcceptedTurnBinding>(Required(acceptedState, "Binding"));
            var before = ReadCanonicalWound(fs, woundId);
            var history = WoundHistoryState.Parse(File.ReadAllText(fs.ResolvePath(WoundHistoryState.HistoryPath)), WoundHistoryState.HistoryPath);
            Assert.True(history.IsValid, Issues(history.Issues));
            var routeId = Assert.Single(before.Treatment.Routes).RouteId;
            var eventRef = Assert.Single(binding.AcceptedEvents).EventRef;
            var treatmentPlanner = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.MortalWoundTreatmentPlanner", false, false);
            Assert.True(treatmentPlanner is not null, "T067 treatment planner is absent.");
            var prepared = Invoke(ExactStatic(treatmentPlanner!, "PrepareProcedureRequest", 6),
                acceptedState, history, before, "operation_t062_stabilize", routeId, eventRef);
            var request = Required(prepared, "Request");
            AssertClosed(prepared, "Issues", "Request");
            var resolutionResult = Invoke(ExactStatic(treatmentPlanner, "CreateProcedureAttempt", 4),
                request, history, before, acceptedState);
            AssertClosed(resolutionResult, "Disposition", "Issues", "ReplayReceipt", "Resolution");
            Assert.Equal("Resolved", Convert.ToString(Required(resolutionResult, "Disposition")));
            Assert.Empty(Values(resolutionResult, "Issues"));
            var resolution = Required(resolutionResult, "Resolution");
            var governedBefore = CaptureGovernedBytes(fs);
            var composed = Invoke(compose, fs, lease, new GameResponse(), acceptedState, request, resolution);
            var plan = Assert.IsType<AcceptedMechanicsPlan>(Required(composed, "Plan"));
            AssertPublishedWoundPlan(plan, Assert.IsType<AcceptedMechanicsWoundStageBundle>(Required(composed, "WoundStageBundle")));
            AssertGovernedBytesUnchanged(fs, governedBefore);
            Assert.Same(plan, new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAcceptedMechanicsAsync(null).GetAwaiter().GetResult());
            Assert.NotEqual("untreated", ReadCanonicalWound(fs, woundId).Care.State);
        }

        private static IReadOnlyDictionary<string, byte[]?> CaptureGovernedBytes(FileSystemManager fs) =>
            new[]
            {
                WoundCarrierCatalog.PlayerPath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
                EffectAcceptedTurnInputComposer.WorldTimePath
            }.ToDictionary(path => path, path =>
            {
                var full = fs.ResolvePath(path);
                return File.Exists(full) ? File.ReadAllBytes(full) : null;
            }, StringComparer.Ordinal);

        private static void AssertGovernedBytesUnchanged(
            FileSystemManager fs,
            IReadOnlyDictionary<string, byte[]?> before)
        {
            foreach (var pair in before)
            {
                var full = fs.ResolvePath(pair.Key);
                if (pair.Value is null) Assert.False(File.Exists(full), pair.Key);
                else Assert.Equal(pair.Value, File.ReadAllBytes(full));
            }
        }

        internal const string CanonicalDeteriorationPolicyPath =
            "game_state/player/wounds.json.activeWounds[0].recovery.deteriorationPolicy";

        private static void WriteTurnRequest(FileSystemManager fs, WoundAcceptedTurnBinding binding) => Write(fs,
            LiveTurnPreparationService.TurnRequestPath, new JsonObject
            {
                ["sessionId"] = binding.SessionId, ["requestId"] = binding.RequestId,
                ["turnNumber"] = binding.Turn, ["gameMode"] = "normal",
                ["preGeneratedDices1d20"] = new JsonArray(17)
            });

        private static void AssertLiveTurnCorrelation(
            FileSystemManager fs,
            WoundAcceptedTurnBinding binding,
            WoundAcceptedTurnInput? input)
        {
            var turn = JsonNode.Parse(File.ReadAllText(fs.ResolvePath(LiveTurnPreparationService.TurnRequestPath)))!.AsObject();
            Assert.Equal(binding.SessionId, turn["sessionId"]!.GetValue<string>());
            Assert.Equal(binding.RequestId, turn["requestId"]!.GetValue<string>());
            Assert.Equal(binding.Turn, turn["turnNumber"]!.GetValue<int>());
            Assert.Equal("mortal_world", binding.Realm);
            Assert.NotEqual(string.Empty, binding.SnapshotToken);
            Assert.Equal(WoundAcceptedEventSetFingerprint.Compute(binding.AcceptedEvents), binding.AcceptedEventsFingerprint);
            Assert.NotEmpty(binding.AcceptedEvents);
            if (input is not null)
            {
                Assert.Equal(binding.SessionId, input.Binding.SessionId);
                Assert.Equal(binding.RequestId, input.Binding.RequestId);
                Assert.Equal(binding.SnapshotToken, input.Binding.SnapshotToken);
                Assert.Equal(binding.Realm, input.Binding.Realm);
                Assert.Equal(binding.Turn, input.Binding.Turn);
                Assert.Equal(binding.AcceptedEventsFingerprint, input.Binding.AcceptedEventsFingerprint);
            }
        }

        private static WoundAcceptedTurnBinding ExportRecoveryBinding(
            FileSystemManager fs,
            FileSystemManager.CanonicalWriteLease lease,
            string woundId)
        {
            var acceptedState = ExportAcceptedState(fs, lease, woundId);
            return Assert.IsType<WoundAcceptedTurnBinding>(Required(acceptedState, "Binding"));
        }

        private static object ExportAcceptedState(
            FileSystemManager fs,
            FileSystemManager.CanonicalWriteLease lease,
            string woundId)
        {
            var treatment = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.MortalWoundTreatmentAuthority", false, false);
            var exportType = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.MortalWoundTreatmentAcceptedStateAuthority", false, false);
            Assert.True(treatment is not null && exportType is not null, "T066 accepted-state authority is absent.");
            var parse = ExactStatic(treatment!, "ParseContext", 2);
            var parsed = Invoke(parse, RecoverySelectionContext(woundId).ToJsonString(), "recoveryContext");
            AssertClosed(parsed, "Context", "IsValid", "Issues");
            Assert.True(Assert.IsType<bool>(Required(parsed, "IsValid")), Issues(Values(parsed, "Issues").Select(Assert.IsType<ValidationIssue>)));
            var context = Required(parsed, "Context");
            var export = ExactStatic(exportType!, "ExportCurrent", 4);
            Assert.Equal(typeof(FileSystemManager), export.GetParameters()[0].ParameterType);
            Assert.Equal(lease.GetType(), export.GetParameters()[1].ParameterType);
            Assert.Equal(context.GetType(), export.GetParameters()[2].ParameterType);
            Assert.Equal(typeof(string), export.GetParameters()[3].ParameterType);
            var result = Invoke(export, fs, lease, context, woundId);
            AssertClosed(result, "Authority", "IsValid", "Issues");
            Assert.True(Assert.IsType<bool>(Required(result, "IsValid")), Issues(Values(result, "Issues").Select(Assert.IsType<ValidationIssue>)));
            return Required(result, "Authority");
        }

        private static JsonObject RecoverySelectionContext(string woundId) => new()
        {
            ["schemaVersion"] = 1, ["realm"] = "mortal_world",
            ["providerKind"] = "npc", ["providerId"] = "field_medic_01",
            ["targetKind"] = "player", ["targetId"] = "player_current",
            ["currentLocationId"] = "loc_field_clinic_001"
        };

        private static WoundMaterializationEnvelope ReadCanonicalWound(FileSystemManager fs, string woundId)
        {
            var player = JsonNode.Parse(File.ReadAllText(fs.ResolvePath(WoundCarrierCatalog.PlayerPath)))!.AsObject();
            var catalog = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(player, null, null, null, null));
            Assert.True(catalog.TryResolveOne(woundId, out var occurrence));
            return occurrence.Wound;
        }

        private static void PrepareLiveTurn(FileSystemManager fs, int turn, string operation) =>
            new LiveTurnPreparationService(fs).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = "session_t062", RequestId = $"request_t062_{turn}", TurnNumber = turn,
                CurrentRealm = "Mortal World", PlayerAction = "T062 " + operation,
                PreGeneratedDices1d20 = new[] { 17 }
            }).GetAwaiter().GetResult();

        private JsonObject? Read(string path)
        {
            var full = FileSystem.ResolvePath(path);
            return File.Exists(full) ? JsonNode.Parse(File.ReadAllText(full))!.AsObject() : null;
        }

        private static void Write(FileSystemManager fs, string path, JsonObject root)
        {
            var full = fs.ResolvePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, root.ToJsonString());
        }
    }
}
