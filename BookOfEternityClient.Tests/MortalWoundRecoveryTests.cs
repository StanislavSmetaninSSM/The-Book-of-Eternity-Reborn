using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
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
        Scenario.DueMinusOne(), Scenario.AtDueBoundary(), Scenario.DuePlusOne(),
        Scenario.MultiCadenceJump(), Scenario.StabilizationRebasesCadence(),
        Scenario.GraceMinusOne(), Scenario.Grace(), Scenario.GracePlusOne(),
        Scenario.RequiresStabilization(), Scenario.NoNaturalRecovery(),
        Scenario.CheckedOverflow(), Scenario.RecoveryNextAnchorOverflow(),
        Scenario.DeteriorationCadenceOverflow(),
        Scenario.DeteriorationNextAnchorOverflow(),
        Scenario.DeteriorationMultiCadence(), Scenario.DeathHandoffRequired(), Scenario.Replay()
    }.Select(static value => new object[] { value });

    public static IEnumerable<object[]> PolicyRows => new[]
    {
        Scenario.StrictlyWorsening(), Scenario.NeutralPolicy(), Scenario.BeneficialPolicy()
    }.Select(static value => new object[] { value });

    public static IEnumerable<object[]> NonReplayPlannerAuthorityRows => new[]
    {
        Scenario.DueMinusOne() with { StartsStabilized = false },
        Scenario.AtDueBoundary() with { StartsStabilized = false },
        Scenario.DuePlusOne() with { StartsStabilized = false },
        Scenario.MultiCadenceJump() with { StartsStabilized = false },
        Scenario.GraceMinusOne(),
        Scenario.Grace(),
        Scenario.GracePlusOne(),
        Scenario.RequiresStabilization(),
        Scenario.NoNaturalRecovery() with { StartsStabilized = false },
        Scenario.CheckedOverflow() with { StartsStabilized = false },
        Scenario.RecoveryNextAnchorOverflow() with { StartsStabilized = false },
        Scenario.DeteriorationCadenceOverflow(),
        Scenario.DeteriorationNextAnchorOverflow(),
        Scenario.DeteriorationMultiCadence(),
        Scenario.DeathHandoffRequired(),
        Scenario.InactiveStrictPolicy(),
        Scenario.ConcurrentIndependentCadences()
    }.Select(static value => new object[] { value });

    public static IEnumerable<object[]> InactiveNonWorseningPolicyRows => new[]
    {
        Scenario.InactiveNeutralPolicy(),
        Scenario.InactiveBeneficialPolicy()
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
        Assert.Contains(Values(result, "Issues").Select(Assert.IsType<ValidationIssue>), issue =>
            issue.Code == "wound_materialization_client_owned_field" &&
            issue.FilePath == "wound.recovery.deteriorationAnchor");

        var nullAnchors = WoundContractTestData.CreateActiveWound();
        nullAnchors["recovery"]!["recoveryAnchor"] = null;
        nullAnchors["recovery"]!["deteriorationAnchor"] = null;
        var allowed = Invoke(validate, nullAnchors, "wound");
        AssertClosed(allowed, "IsValid", "Issues");
        Assert.True(Assert.IsType<bool>(Required(allowed, "IsValid")));
        Assert.Empty(Values(allowed, "Issues"));

        foreach (var anchor in new[] { "recoveryAnchor", "deteriorationAnchor" })
        {
            var absentOnly = WoundContractTestData.CreateActiveWound();
            absentOnly["recovery"]!.AsObject().Remove(anchor);
            var absentResult = Invoke(validate, absentOnly, "wound");
            AssertClosed(absentResult, "IsValid", "Issues");
            Assert.True(Assert.IsType<bool>(Required(absentResult, "IsValid")), anchor);
            Assert.Empty(Values(absentResult, "Issues"));
        }
    }

    [Theory]
    [MemberData(nameof(NonReplayPlannerAuthorityRows))]
    public void T069B_PlannerUsesOnlyCanonicalNonReplayAuthorityWithoutWriting(
        Scenario scenario)
    {
        using var fixture = Fixture.Create(scenario);
        fixture.AssertCarrierIdentityHistoryAgreement();
        var governedBefore = Fixture.CaptureAllGovernedBytes(fixture.FileSystem);
        var treeBefore = fixture.CaptureCanonicalTreeBytes();

        var first = AssertPlannerResult(
            InvokePlan(fixture),
            scenario,
            fixture.WoundId);
        var repeated = AssertPlannerResult(
            InvokePlan(fixture),
            scenario,
            fixture.WoundId);
        if (first is not null)
        {
            Assert.NotNull(repeated);
            Assert.Equal(
                Required(first, "AuthorityFingerprint"),
                Required(repeated!, "AuthorityFingerprint"));
            Assert.Equal(
                Required(first, "TickKey"),
                Required(repeated, "TickKey"));
        }

        Fixture.AssertAllGovernedBytesUnchanged(
            fixture.FileSystem,
            governedBefore);
        fixture.AssertCanonicalTreeBytesUnchanged(treeBefore);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out _));
    }

    [Fact]
    public void T069B_PlannerRejectsStaleAcceptedBindingWithoutWriting()
    {
        using var fixture = Fixture.Create(Scenario.RequiresStabilization());
        var staleBinding = fixture.Binding;
        fixture.PrepareFreshContinuationTurn(45, "stale_recovery_binding");
        var governedBefore = Fixture.CaptureAllGovernedBytes(fixture.FileSystem);
        var treeBefore = fixture.CaptureCanonicalTreeBytes();

        AssertPlannerAuthorityRejected(InvokePlan(fixture, staleBinding));

        Fixture.AssertAllGovernedBytesUnchanged(
            fixture.FileSystem,
            governedBefore);
        fixture.AssertCanonicalTreeBytesUnchanged(treeBefore);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out _));
    }

    [Fact]
    public void T069B_PlannerRejectsMismatchedWoundIdWithoutWriting()
    {
        using var fixture = Fixture.Create(Scenario.RequiresStabilization());
        var governedBefore = Fixture.CaptureAllGovernedBytes(fixture.FileSystem);
        var treeBefore = fixture.CaptureCanonicalTreeBytes();

        AssertPlannerAuthorityRejected(InvokePlan(
            fixture,
            woundId: "wound_other_current"));

        Fixture.AssertAllGovernedBytesUnchanged(
            fixture.FileSystem,
            governedBefore);
        fixture.AssertCanonicalTreeBytesUnchanged(treeBefore);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out _));
    }

    [Theory]
    [MemberData(nameof(InactiveNonWorseningPolicyRows))]
    public void T069B_PlannerRejectsInactiveNonWorseningPolicyWithoutWriting(
        Scenario scenario)
    {
        using var fixture = Fixture.Create(scenario);
        var governedBefore = Fixture.CaptureAllGovernedBytes(fixture.FileSystem);
        var treeBefore = fixture.CaptureCanonicalTreeBytes();

        var result = InvokePlan(fixture);

        AssertClosed(result, "Disposition", "Issues", "ReplayReceipt", "Resolution");
        Assert.Equal("Rejected", Convert.ToString(Required(result, "Disposition")));
        var issue = Assert.Single(
            Values(result, "Issues").Select(Assert.IsType<ValidationIssue>));
        Assert.Equal(
            "mortal_wound_deterioration_policy_not_strictly_worsening",
            issue.Code);
        Assert.Equal(Fixture.CanonicalDeteriorationPolicyPath, issue.FilePath);
        Assert.Null(Optional(result, "ReplayReceipt"));
        Assert.Null(Optional(result, "Resolution"));
        Fixture.AssertAllGovernedBytesUnchanged(
            fixture.FileSystem,
            governedBefore);
        fixture.AssertCanonicalTreeBytesUnchanged(treeBefore);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out _));
    }

    [Fact]
    public void DeteriorationAnchor_ClearsOnSealedStabilizationAndReallocatesOnSealedConditionReentry()
    {
        using var fixture = Fixture.Create(Scenario.DeteriorationAnchorLifecycle());

        var initial = Assert.IsType<byte[]>(fixture.InitialDeteriorationAnchorBytes);
        Assert.Null(fixture.PostStabilizationDeteriorationAnchorBytes);
        var initialAnchor = JsonNode.Parse(initial)!.AsObject();
        Assert.Equal("not_stabilized", initialAnchor["conditionKey"]!.GetValue<string>());
        Assert.Equal(100, initialAnchor["anchorMinute"]!.GetValue<long>());
        var stabilizedRecoveryAnchor = Assert.IsType<byte[]>(
            fixture.CaptureRecoveryAnchorBytes());
        Assert.Equal(
            150,
            JsonNode.Parse(stabilizedRecoveryAnchor)!["anchorMinute"]!.GetValue<long>());

        var publication = fixture.PublishSealedWorseningReentry(turn: 45, minute: 170);

        var reentered = Assert.IsType<byte[]>(fixture.CaptureDeteriorationAnchorBytes());
        Assert.False(initial.AsSpan().SequenceEqual(reentered));
        var reenteredAnchor = JsonNode.Parse(reentered)!.AsObject();
        Assert.Equal("not_stabilized", reenteredAnchor["conditionKey"]!.GetValue<string>());
        Assert.Equal(170, reenteredAnchor["anchorMinute"]!.GetValue<long>());
        Assert.Equal(
            publication.WorseningTransitionId,
            reenteredAnchor["anchorTransitionId"]!.GetValue<string>());
        Assert.False(
            string.Equals(
                initialAnchor["anchorTransitionId"]!.GetValue<string>(),
                publication.WorseningTransitionId,
                StringComparison.Ordinal));
        Assert.True(stabilizedRecoveryAnchor.AsSpan().SequenceEqual(
            Assert.IsType<byte[]>(fixture.CaptureRecoveryAnchorBytes())));
        fixture.AssertCarrierIdentityHistoryAgreement();
    }

    [Theory]
    [MemberData(nameof(PlannerRows))]
    public void Plan_UsesCanonicalAnchorClockAndClosedTransitionIntents(Scenario scenario)
    {
        using var fixture = Fixture.Create(scenario);
        fixture.AssertCarrierIdentityHistoryAgreement();

        var rejectedBefore = Fixture.CaptureAllGovernedBytes(fixture.FileSystem);
        var rejectedTreeBefore = fixture.CaptureCanonicalTreeBytes();
        var resolution = AssertPlannerResult(InvokePlan(fixture), scenario, fixture.WoundId);
        if (resolution is null)
        {
            Fixture.AssertAllGovernedBytesUnchanged(fixture.FileSystem, rejectedBefore);
            fixture.AssertCanonicalTreeBytesUnchanged(rejectedTreeBefore);
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.FileSystem, fixture.Lease, out _, out _));
        }
        if (resolution is null)
            return;
        var receipt = ComposeAndPublishRecovery(fixture, resolution!);
        var recoveryAfterPublish = fixture.CaptureRecoveryStateBytes();
        fixture.AssertCarrierIdentityHistoryAgreement();
        if (!scenario.ReplayAfterCommit)
            return;
        var firstBinding = fixture.Binding;
        fixture.PrepareFreshContinuationTurn(45, "same_clock_replay");
        Assert.NotEqual(firstBinding.Turn, fixture.Binding.Turn);
        var sameClockTree = fixture.CaptureCanonicalTreeBytes();
        AssertExactReplay(InvokePlan(fixture), receipt);
        fixture.AssertCanonicalTreeBytesUnchanged(sameClockTree);
        fixture.RestartForReplay();
        fixture.AssertCarrierIdentityHistoryAgreement();
        fixture.AssertRecoveryStateBytesEqual(recoveryAfterPublish);
        fixture.CorruptLiveClock();
        AssertExactReplay(InvokePlan(fixture), receipt);
    }

    [Theory]
    [InlineData("receipt")]
    [InlineData("history")]
    public void Replay_TamperedDurableEvidenceDominatesLiveClock(string tamper)
    {
        using var fixture = Fixture.Create(Scenario.Replay());
        var resolution = AssertPlannerResult(InvokePlan(fixture), Scenario.Replay(), fixture.WoundId);
        Assert.NotNull(resolution);
        var receipt = ComposeAndPublishRecovery(fixture, resolution!);
        fixture.PrepareFreshContinuationTurn(45, "tamper_replay");
        fixture.RestartForReplay();
        fixture.TamperPersistedRecoveryEvidence(tamper);
        fixture.CorruptLiveClock();

        AssertInvalidHistory(InvokePlan(fixture));
        AssertReceipt(receipt, fixture.WoundId);
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

    [Theory]
    [InlineData("missing_provider")]
    [InlineData("stale_skill")]
    [InlineData("missing_item")]
    [InlineData("mismatched_location")]
    [InlineData("withdrawn_consent")]
    public void AcceptedStateExport_RejectsCanonicalTreatmentAuthorityFaultBeforeComposition(
        string mutation)
    {
        using var fixture = Fixture.Create(Scenario.AtDueBoundary());
        fixture.MutateTreatmentAuthorityRoot(mutation);
        var before = fixture.CaptureCanonicalTreeBytes();

        var exported = fixture.ExportCurrentResult();

        AssertClosed(exported, "Authority", "IsValid", "Issues");
        Assert.False(Assert.IsType<bool>(Required(exported, "IsValid")));
        Assert.Null(Optional(exported, "Authority"));
        Assert.NotEmpty(Values(exported, "Issues"));
        fixture.AssertCanonicalTreeBytesUnchanged(before);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem, fixture.Lease, out _, out _));
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

    private static object InvokePlan(
        Fixture fixture,
        WoundAcceptedTurnBinding? binding = null,
        string? woundId = null)
    {
        var type = typeof(WoundMaterializationContract).Assembly.GetType(PlannerName, false, false);
        Assert.True(type is not null, $"T069 planner is absent for '{fixture.Name}'.");
        var method = Assert.Single(
            type!.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => candidate.Name == "Plan");
        Assert.Equal(4, method.GetParameters().Length);
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
        return Invoke(
            method,
            fixture.FileSystem,
            fixture.Lease,
            binding ?? fixture.Binding,
            woundId ?? fixture.WoundId);
    }

    private static void AssertPlannerAuthorityRejected(object result)
    {
        AssertClosed(result, "Disposition", "Issues", "ReplayReceipt", "Resolution");
        Assert.Equal("Rejected", Convert.ToString(Required(result, "Disposition")));
        var issue = Assert.Single(
            Values(result, "Issues").Select(Assert.IsType<ValidationIssue>));
        Assert.Equal("mortal_wound_recovery_authority_invalid", issue.Code);
        Assert.Null(Optional(result, "ReplayReceipt"));
        Assert.Null(Optional(result, "Resolution"));
    }

    private static object? AssertPlannerResult(object result, Scenario scenario, string woundId)
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
        AssertResolution(resolution!, scenario, woundId);
        return resolution;
    }

    private static void AssertResolution(object resolution, Scenario scenario, string woundId)
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
        Assert.All(intents, intent => AssertTypedIntent(intent, scenario, woundId,
            Required(resolution, "TickKey"), Required(resolution, "AuthorityFingerprint")));
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

    private static void AssertTypedIntent(object value, Scenario scenario, string woundId, object tickKey, object authorityFingerprint)
    {
        Assert.False(value is JsonNode);
        Assert.Contains(value.GetType().Name, new[]
        {
            "MortalWoundRecoveryProgressIntent", "MortalWoundRecoveryDeteriorationIntent",
            "MortalWoundDeathHandoffIntent"
        });
        AssertClosedIntent(value);
        Assert.Equal(tickKey, Required(value, "TickKey"));
        Assert.Equal(woundId, Required(value, "WoundId"));
        Assert.Equal(authorityFingerprint, Required(value, "AuthorityFingerprint"));
        if (value.GetType().Name == "MortalWoundRecoveryProgressIntent")
        {
            Assert.Equal(scenario.ElapsedCadences, Assert.IsType<long>(Required(value, "ElapsedCadences")));
            Assert.Equal(scenario.NextRecoveryAnchor, NullableLong(value, "NextRecoveryAnchorMinute"));
        }
        if (value.GetType().Name == "MortalWoundRecoveryDeteriorationIntent")
        {
            Assert.Equal(scenario.ElapsedDeteriorationCadences, Assert.IsType<long>(Required(value, "ElapsedCadences")));
            Assert.Equal(scenario.PolicyRefExpected, NullableString(value, "PolicyRef"));
            Assert.Equal(scenario.NextDeteriorationAnchor, NullableLong(value, "NextDeteriorationAnchorMinute"));
        }
        if (value.GetType().Name == "MortalWoundDeathHandoffIntent")
            Assert.Equal(scenario.PolicyRefExpected, NullableString(value, "PolicyRef"));
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
        var before = Fixture.CaptureAllGovernedBytes(fixture.FileSystem);
        var wholeTreeBefore = fixture.CaptureCanonicalTreeBytes();
        var deteriorationAnchorBefore = fixture.CaptureDeteriorationAnchorBytes();
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
        Assert.Equal(fixture.Binding.SessionId, bundle.Input.Binding.SessionId);
        Assert.Equal(fixture.Binding.RequestId, bundle.Input.Binding.RequestId);
        Assert.Equal(fixture.Binding.SnapshotToken, bundle.Input.Binding.SnapshotToken);
        Assert.Equal(fixture.Binding.Realm, bundle.Input.Binding.Realm);
        Assert.Equal(fixture.Binding.Turn, bundle.Input.Binding.Turn);
        Assert.Equal(fixture.Binding.AcceptedEventsFingerprint, bundle.Input.Binding.AcceptedEventsFingerprint);
        Assert.Equal(fixture.Binding.AcceptedEvents, bundle.Input.Binding.AcceptedEvents);
        Assert.Equal(fixture.Binding.AcceptedEvents, bundle.FinalPlan.Binding.AcceptedEvents);
        Assert.Equal(fixture.Binding.Realm, bundle.FinalPlan.Binding.Realm);
        Assert.Equal(fixture.Binding.Turn, bundle.FinalPlan.Binding.Turn);
        var acceptedPlan = Assert.IsType<AcceptedMechanicsPlan>(Required(result, "AcceptedPlan"));
        AssertPublishedWoundPlan(acceptedPlan, bundle, fixture.WoundId);
        Fixture.AssertComposeDidNotWrite(fixture.FileSystem, before, acceptedPlan);
        fixture.AssertCanonicalTreeBytesUnchanged(wholeTreeBefore);
        var receipt = Required(result, "Receipt");
        AssertReceipt(receipt, fixture.WoundId, resolution);
        Assert.Same(acceptedPlan, PublishAcceptedPlan(fixture));
        Fixture.AssertAcceptedPlanPublishedExactly(fixture.FileSystem, before, acceptedPlan);
        if (!fixture.ExpectsDeteriorationIntent)
            fixture.AssertDeteriorationAnchorBytesEqual(deteriorationAnchorBefore);
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

    private static void AssertInvalidHistory(object result)
    {
        AssertClosed(result, "Disposition", "Issues", "ReplayReceipt", "Resolution");
        Assert.Equal("InvalidHistory", Convert.ToString(Required(result, "Disposition")));
        Assert.NotEmpty(Values(result, "Issues"));
        Assert.Null(Optional(result, "ReplayReceipt"));
        Assert.Null(Optional(result, "Resolution"));
    }

    private static void AssertReceipt(object receipt, string? expectedWoundId = null, object? resolution = null)
    {
        Assert.False(receipt is JsonNode);
        AssertClosed(receipt, "AuthorityFingerprint", "ReceiptFingerprint", "TickKey", "WoundId");
        Assert.NotEqual(string.Empty, Assert.IsType<string>(Required(receipt, "TickKey")));
        AssertFingerprint(Required(receipt, "ReceiptFingerprint"));
        AssertFingerprint(Required(receipt, "AuthorityFingerprint"));
        if (expectedWoundId is not null)
            Assert.Equal(expectedWoundId, Required(receipt, "WoundId"));
        if (resolution is not null)
        {
            Assert.Equal(Required(resolution, "TickKey"), Required(receipt, "TickKey"));
            Assert.Equal(Required(resolution, "AuthorityFingerprint"), Required(receipt, "AuthorityFingerprint"));
        }
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
        AcceptedMechanicsWoundStageBundle? expectedBundle,
        string? selectedWoundId = null)
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
        if (selectedWoundId is null)
            return;

        var carriers = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            plan.WoundCarrierAfterImages[WoundCarrierCatalog.PlayerPath], null, null, null, null));
        Assert.Empty(carriers.Issues);
        Assert.True(carriers.TryResolveOne(selectedWoundId, out var occurrence));
        var identity = WoundIdentityState.Parse(
            plan.WoundIdentityAfterImage!.ToJsonString(), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(
            plan.WoundHistoryAfterImage!.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(identity.IsValid, Issues(identity.Issues));
        Assert.True(history.IsValid, Issues(history.Issues));
        Assert.True(identity.State!.TryGetEntry(selectedWoundId, out var entry));
        Assert.Empty(WoundIdentityState.ValidateActiveAgreement(entry!, occurrence.Wound, "recoveryPlan"));
        Assert.Empty(history.State!.ValidateAgreement(identity.State, carriers));
        var final = actual.FinalPlan;
        Assert.Contains(selectedWoundId, final.AllocatedWoundIds);
        Assert.Contains(final.TransitionIntents, value => value switch
        {
            WoundCarrierTransitionIntent carrier => carrier.WoundId == selectedWoundId,
            WoundEffectTransitionIntent effect => effect.WoundId == selectedWoundId,
            WoundTransitionHistoryIntent historyIntent => historyIntent.WoundId == selectedWoundId,
            WoundAttemptTerminalIntent attempt => attempt.WoundId == selectedWoundId,
            WoundRecoverySealIntent recovery => recovery.WoundId == selectedWoundId,
            WoundFollowUpHealIntent heal => heal.WoundId == selectedWoundId,
            WoundArchiveProjectionIntent archive => archive.WoundId == selectedWoundId,
            _ => false
        });
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

    private sealed record WorseningReentryPublication(string WorseningTransitionId);

    public sealed record Scenario(
        string Name, string Mode, long Anchor, long? DeteriorationAnchor, long CreationMinute,
        long? StabilizationMinute, long Minute, JsonObject Wound, JsonObject WorldTime,
        string Disposition, string RecoveryDisposition, string? PolicyRefExpected, long? Due,
        long? GraceDeadline, long ElapsedCadences, long ElapsedDeteriorationCadences,
        long? NextRecoveryAnchor, long? NextDeteriorationAnchor, string[] IntentTypes,
        bool DeathHandoff, bool ReplayAfterCommit,
        bool PolicyValid, bool StartsStabilized, bool NoMechanics)
    {
        internal static Scenario DueMinusOne() => Create("cadence_due_minus_one", "progressive", 109,
            "NotDue", null, 110, null, [], elapsedCadences: 0);
        internal static Scenario AtDueBoundary() => Create("cadence_due", "progressive", 110,
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
        internal static Scenario RecoveryNextAnchorOverflow() => Create("checked_next_recovery_anchor_overflow", "progressive", long.MaxValue,
            "", null, null, null, [], expected: "Rejected", anchor: long.MaxValue - 30, cadence: 10,
            elapsedCadences: 0, creationMinute: long.MaxValue - 30);
        internal static Scenario DeteriorationCadenceOverflow() => Create("checked_deterioration_cadence_overflow", "requires_stabilization", long.MaxValue,
            "", null, null, null, [], "increase_severity", stabilized: false, expected: "Rejected",
            deteriorationAnchor: long.MaxValue - 5, elapsedCadences: 0, elapsedDeteriorationCadences: 0,
            creationMinute: long.MaxValue - 5);
        internal static Scenario DeteriorationNextAnchorOverflow() => Create("checked_deterioration_next_anchor_overflow", "requires_stabilization", long.MaxValue,
            "", null, null, null, [], "increase_severity", stabilized: false, expected: "Rejected",
            deteriorationAnchor: long.MaxValue - 40, elapsedCadences: 0, elapsedDeteriorationCadences: 0,
            creationMinute: long.MaxValue - 40, graceMinutes: 10);
        internal static Scenario DeteriorationMultiCadence() => Create("deterioration_multi_cadence", "requires_stabilization", 155,
            "Deteriorated", "untreated_infection", 110, 130, ["MortalWoundRecoveryDeteriorationIntent"], "increase_severity",
            stabilized: false, elapsedCadences: 0, elapsedDeteriorationCadences: 3, nextDeteriorationAnchor: 160);
        internal static Scenario DeathHandoffRequired() => Create("death_is_lifecycle_handoff", "requires_stabilization", 130,
            "DeathHandoffRequired", "untreated_infection", 110, 130, ["MortalWoundDeathHandoffIntent"], "death_contour", stabilized: false,
            death: true, elapsedCadences: 0, elapsedDeteriorationCadences: 1);
        internal static Scenario Replay() => Create("replay_precedes_malformed_clock", "progressive", 110,
            "Progressed", null, 110, null, ["MortalWoundRecoveryProgressIntent"], replay: true);
        internal static Scenario DeteriorationAnchorLifecycle() => Create(
            "deterioration_anchor_clear_and_reentry", "requires_stabilization", 160,
            "NotDue", null, 110, 130, [], "increase_severity", "untreated_infection",
            stabilized: true, stabilizationMinute: 150, anchor: 150, noMechanics: true);
        internal static Scenario StrictlyWorsening() => Create("strictly_worsening_interruption", "requires_stabilization", 130,
            "Deteriorated", "missed_course_dose", 110, 130, ["MortalWoundRecoveryDeteriorationIntent"],
            "increase_severity", "missed_course_dose", false, policyValid: true);
        internal static Scenario NeutralPolicy() => Create("neutral_interruption_rejected", "requires_stabilization", 130,
            "", null, 110, 130, [], "no_change", stabilized: false, expected: "Rejected");
        internal static Scenario BeneficialPolicy() => Create("beneficial_interruption_rejected", "requires_stabilization", 130,
            "", null, 110, 130, [], "add_recovery", stabilized: false, expected: "Rejected");
        internal static Scenario InactiveNeutralPolicy() => Create(
            "inactive_neutral_deterioration_rejected", "progressive", 109,
            "", null, 110, null, [], "no_change", stabilized: false,
            expected: "Rejected", elapsedCadences: 0);
        internal static Scenario InactiveBeneficialPolicy() => Create(
            "inactive_beneficial_deterioration_rejected", "progressive", 109,
            "", null, 110, null, [], "add_recovery", stabilized: false,
            expected: "Rejected", elapsedCadences: 0);
        internal static Scenario InactiveStrictPolicy() => Create(
            "inactive_strict_deterioration_retained", "progressive", 109,
            "NotDue", "untreated_infection", 110, null, [],
            "increase_severity", stabilized: false, elapsedCadences: 0);
        internal static Scenario ConcurrentIndependentCadences()
        {
            var scenario = Create(
                "concurrent_independent_recovery_and_deterioration",
                "progressive",
                145,
                "Deteriorated",
                "untreated_infection",
                110,
                130,
                [
                    "MortalWoundRecoveryProgressIntent",
                    "MortalWoundRecoveryDeteriorationIntent"
                ],
                "increase_severity",
                stabilized: false,
                deteriorationAnchor: 100,
                elapsedCadences: 4,
                elapsedDeteriorationCadences: 3,
                nextRecoveryAnchor: 150,
                nextDeteriorationAnchor: 151);
            var recovery = Assert.IsType<JsonObject>(scenario.Wound["recovery"]);
            recovery["blockers"] = new JsonArray("contaminated");
            var policy = Assert.IsType<JsonObject>(recovery["deteriorationPolicy"]);
            policy["unmetConditions"] = new JsonArray("contaminated");
            policy["cadenceMinutes"] = 7;
            return scenario;
        }

        private static Scenario Create(string name, string mode, long minute, string recoveryDisposition,
            string? expectedPolicyRef, long? due, long? grace, string[] intents, string? policyKind = null,
            string? policyRef = null, bool stabilized = true, string expected = "Resolved", long anchor = 100,
            long cadence = 10, bool death = false, bool replay = false, bool policyValid = false,
            long? stabilizationMinute = null, long? deteriorationAnchor = null, long elapsedCadences = 1,
            long elapsedDeteriorationCadences = 0, long? nextRecoveryAnchor = null,
            long? nextDeteriorationAnchor = null, long creationMinute = 100,
            long graceMinutes = 30, bool noMechanics = false)
        {
            var wound = WoundContractTestData.CreateActiveWound();
            // GM data may declare recovery mode and cadence, but not a canonical state
            // transition. The accepted creation is always untreated; when a row needs a
            // stabilized wound, Fixture materializes its second transition through T070.
            wound["care"]!["state"] = "untreated";
            wound["care"]!["stabilizedAtTurn"] = null;
            wound["recovery"] = Recovery(mode, cadence, policyKind, policyRef, graceMinutes);
            var requirements = wound["treatment"]!["routes"]![0]!["requirements"]!.AsArray();
            requirements.Add(new JsonObject { ["kind"] = "provider", ["providerRef"] = "field_medic_01" });
            requirements.Add(new JsonObject
            {
                ["kind"] = "consent", ["consentRef"] = "consent_field_medic_player_01",
                ["providerRef"] = "field_medic_01", ["targetRef"] = "player_current"
            });
            requirements.Add(new JsonObject { ["kind"] = "facility", ["facilityRef"] = "fac_field_clinic_001" });
            requirements.Add(new JsonObject
            {
                ["kind"] = "location", ["locationRef"] = "loc_field_clinic_001", ["targetRole"] = "target"
            });
            var conditionAnchor = deteriorationAnchor ??
                (policyKind is null || mode != "requires_stabilization"
                    ? null
                    : creationMinute);
            var elapsedDeterioration = policyKind is null ? 0 : elapsedDeteriorationCadences;
            return new(name, mode, anchor, conditionAnchor, creationMinute, stabilizationMinute, minute, wound,
                new JsonObject { ["currentTimeInMinutes"] = creationMinute }, expected, recoveryDisposition,
                expectedPolicyRef, due, grace, elapsedCadences, elapsedDeterioration,
                mode == "no_natural_recovery" ? null : nextRecoveryAnchor ?? (expected == "Rejected" ? null :
                    (elapsedCadences == 0 ? anchor + cadence : anchor + (elapsedCadences + 1) * cadence)),
                nextDeteriorationAnchor ?? (conditionAnchor is null
                    ? null
                    : conditionAnchor + graceMinutes +
                      elapsedDeterioration * cadence),
                intents, death, replay, policyValid, stabilized, noMechanics);
        }
    }

    private static JsonObject Recovery(string mode, long cadence, string? policyKind, string? policyRef,
        long graceMinutes = 30) => new()
    {
        ["mode"] = mode, ["clockKind"] = "world_time.currentTimeInMinutes",
        ["cadence"] = cadence, ["currentStepProgress"] = 0, ["currentStepThreshold"] = 1,
        ["lastTickKey"] = null,
        ["blockers"] = mode == "requires_stabilization" ? new JsonArray("not_stabilized") : new JsonArray(),
        ["carryOverflow"] = true,
        ["deteriorationPolicy"] = policyKind is null ? null : new JsonObject
        {
            ["policyRef"] = policyRef ?? "untreated_infection", ["unmetConditions"] = new JsonArray("not_stabilized"),
            ["graceMinutes"] = graceMinutes, ["cadenceMinutes"] = 10L,
            ["result"] = new JsonObject { ["kind"] = policyKind }
        }
    };

    private sealed class Fixture : IDisposable
    {
        private Fixture(string root, FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
            WoundAcceptedTurnBinding binding, string woundId, Scenario scenario,
            byte[]? initialDeteriorationAnchorBytes, byte[]? postStabilizationDeteriorationAnchorBytes)
        {
            Root = root;
            FileSystem = fs;
            Lease = lease;
            Binding = binding;
            WoundId = woundId;
            Scenario = scenario;
            InitialDeteriorationAnchorBytes = initialDeteriorationAnchorBytes;
            PostStabilizationDeteriorationAnchorBytes = postStabilizationDeteriorationAnchorBytes;
        }

        private string Root { get; }
        internal FileSystemManager FileSystem { get; private set; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; private set; }
        internal WoundAcceptedTurnBinding Binding { get; private set; }
        internal string WoundId { get; }
        internal byte[]? InitialDeteriorationAnchorBytes { get; }
        internal byte[]? PostStabilizationDeteriorationAnchorBytes { get; }
        internal string Name => Scenario.Name;
        internal string PolicyRef => Assert.IsType<JsonObject>(Scenario.Wound["recovery"]!["deteriorationPolicy"])["policyRef"]!.GetValue<string>();
        internal bool ExpectsDeteriorationIntent => Scenario.IntentTypes.Contains(
            "MortalWoundRecoveryDeteriorationIntent", StringComparer.Ordinal);
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
                WriteCanonicalTreatmentAuthorityRoots(fs);
                AssertCanonicalTreatmentAuthorityRoots(fs);
                var creationInput = scenario.NoMechanics
                    ? WoundEffectBatchPlannerTests.CreateNoMechanicsInputForAcceptedCache()
                    : WoundEffectBatchPlannerTests.CreateInputForAcceptedCache();
                Write(fs, EffectAcceptedTurnInputComposer.WorldTimePath, scenario.WorldTime);
                creationInput = PrepareSignedCreationTurn(fs, creationInput);
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
                var initialDeteriorationAnchorBytes = CaptureDeteriorationAnchorBytes(fs);
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
                var postStabilizationDeteriorationAnchorBytes =
                    CaptureDeteriorationAnchorBytes(fs);
                Write(fs, EffectAcceptedTurnInputComposer.WorldTimePath,
                    new JsonObject { ["currentTimeInMinutes"] = scenario.Minute });
                lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
                lease = null;
                PrepareLiveTurn(fs, 44, "recovery");
                lease = fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
                // T066 must export the recovery binding from the current prepared turn;
                // the bootstrap creation binding is never reused for this continuation.
                var recoveryBinding = ExportRecoveryBinding(fs, lease, woundId);
                return new(root, fs, lease, recoveryBinding, woundId, scenario,
                    initialDeteriorationAnchorBytes,
                    postStabilizationDeteriorationAnchorBytes);
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

        internal byte[] CaptureRecoveryStateBytes()
        {
            var player = ReadRoot(FileSystem, WoundCarrierCatalog.PlayerPath);
            var wound = Assert.IsType<JsonObject>(Assert.Single(
                player["activeWounds"]!.AsArray()));
            return System.Text.Encoding.UTF8.GetBytes(wound["recovery"]!.ToJsonString());
        }

        internal void AssertRecoveryStateBytesEqual(byte[] expected) =>
            Assert.True(expected.AsSpan().SequenceEqual(CaptureRecoveryStateBytes()));

        internal byte[]? CaptureDeteriorationAnchorBytes()
            => CaptureDeteriorationAnchorBytes(FileSystem);

        internal byte[]? CaptureRecoveryAnchorBytes()
        {
            var player = ReadRoot(FileSystem, WoundCarrierCatalog.PlayerPath);
            var wound = Assert.IsType<JsonObject>(Assert.Single(
                player["activeWounds"]!.AsArray()));
            return wound["recovery"]?["recoveryAnchor"] is { } anchor
                ? System.Text.Encoding.UTF8.GetBytes(anchor.ToJsonString())
                : null;
        }

        private static byte[]? CaptureDeteriorationAnchorBytes(FileSystemManager fs)
        {
            var player = ReadRoot(fs, WoundCarrierCatalog.PlayerPath);
            var wound = Assert.IsType<JsonObject>(Assert.Single(
                player["activeWounds"]!.AsArray()));
            return wound["recovery"]?["deteriorationAnchor"] is { } anchor
                ? System.Text.Encoding.UTF8.GetBytes(anchor.ToJsonString())
                : null;
        }

        internal void AssertDeteriorationAnchorBytesEqual(byte[]? expected)
        {
            var actual = CaptureDeteriorationAnchorBytes();
            Assert.Equal(expected is not null, actual is not null);
            if (expected is not null)
                Assert.True(expected.AsSpan().SequenceEqual(Assert.IsType<byte[]>(actual)));
        }

        internal IReadOnlyDictionary<string, byte[]> CaptureCanonicalTreeBytes() =>
            CaptureTreeBytes(Root);

        internal void AssertCanonicalTreeBytesUnchanged(
            IReadOnlyDictionary<string, byte[]> before)
        {
            var after = CaptureCanonicalTreeBytes();
            Assert.Equal(before.Keys.OrderBy(static path => path, StringComparer.Ordinal),
                after.Keys.OrderBy(static path => path, StringComparer.Ordinal));
            foreach (var pair in before)
            {
                Assert.True(after.TryGetValue(pair.Key, out var actual), pair.Key);
                Assert.True(pair.Value.AsSpan().SequenceEqual(actual), pair.Key);
            }
        }

        internal void CorruptLiveClock() => Write(FileSystem, EffectAcceptedTurnInputComposer.WorldTimePath,
            new JsonObject { ["currentTimeInMinutes"] = "malformed" });

        internal object ExportCurrentResult() => ExportAcceptedStateResult(FileSystem, Lease, WoundId);

        internal void MutateTreatmentAuthorityRoot(string mutation)
        {
            var npc = ReadRoot(FileSystem, "game_state/npcs/npc_core.json");
            var medic = Assert.IsType<JsonObject>(Assert.Single(
                npc["NPCsInScene"]!.AsArray()));
            switch (mutation)
            {
                case "missing_provider":
                    npc["NPCsInScene"] = new JsonArray();
                    break;
                case "stale_skill":
                    medic["activeSkills"]![0]!["active"] = false;
                    break;
                case "missing_item":
                    medic["inventory"] = new JsonArray();
                    break;
                case "mismatched_location":
                    medic["currentLocationId"] = "loc_elsewhere_001";
                    break;
                case "withdrawn_consent":
                    var currentLocation = ReadRoot(
                        FileSystem,
                        MortalLocationMaterializationContract.CurrentLocationPath);
                    var consent = Assert.Single(
                        currentLocation["customStates"]!.AsArray().OfType<JsonObject>(),
                        static row => string.Equals(
                            row["kind"]?.GetValue<string>(),
                            MortalWoundTreatmentSceneAuthorityContract.ConsentKind,
                            StringComparison.Ordinal));
                    consent["status"] = "withdrawn";
                    Write(
                        FileSystem,
                        MortalLocationMaterializationContract.CurrentLocationPath,
                        currentLocation);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }
            Write(FileSystem, "game_state/npcs/npc_core.json", npc);
        }

        internal void RestartForReplay()
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            FileSystem = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance);
            FileSystem.EnsureDirectoryStructure();
            Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
            Binding = ExportRecoveryBinding(FileSystem, Lease, WoundId);
            AssertCarrierIdentityHistoryAgreement();
        }

        internal void PrepareFreshContinuationTurn(int turn, string operation)
        {
            Write(FileSystem, EffectAcceptedTurnInputComposer.WorldTimePath,
                new JsonObject { ["currentTimeInMinutes"] = Scenario.Minute });
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            PrepareLiveTurn(FileSystem, turn, operation);
            Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
            Binding = ExportRecoveryBinding(FileSystem, Lease, WoundId);
            Assert.Equal(turn, Binding.Turn);
            Assert.Equal($"request_t062_{turn}", Binding.RequestId);
        }

        internal WorseningReentryPublication PublishSealedWorseningReentry(int turn, long minute)
        {
            var before = ReadCanonicalWound(FileSystem, WoundId);
            Assert.Equal("stabilized", before.Care.State);
            Assert.DoesNotContain("not_stabilized", before.Recovery.Blockers);
            const string narration =
                "Повторный удар разрывает уже сведённые края раны; прежняя стабилизация утрачена.";
            var opportunityRef = $"wound-opportunity-t062-retrauma-{turn}";
            var decision = new JsonObject
            {
                ["opportunityRef"] = opportunityRef,
                ["decision"] = "materialize",
                ["woundRef"] = "wound_local_t062_retrauma",
                ["proposal"] = CreateWorseningReentryProposal(before, narration)
            };
            var response = new GameResponse
            {
                Response = narration,
                EffectChanges = Array.Empty<JsonElement>(),
                EffectResolutionReceipts = Array.Empty<JsonElement>(),
                EffectEventReports = Array.Empty<JsonElement>(),
                WoundDecisions = new[] { JsonSerializer.SerializeToElement(decision) }
            };

            Write(FileSystem, EffectAcceptedTurnInputComposer.WorldTimePath,
                new JsonObject { ["currentTimeInMinutes"] = minute });
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            PrepareLiveTurn(FileSystem, turn, "formal combat retrauma");
            Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
            var sourceEvent = CreateWorseningReentrySource(
                before,
                opportunityRef,
                turn);
            var treeBeforeComposition = CaptureCanonicalTreeBytes();
            var composed = ComposeAcceptedWorseningResponse(
                FileSystem,
                Lease,
                sourceEvent,
                response);
            AssertCanonicalTreeBytesUnchanged(treeBeforeComposition);
            Assert.True(composed.Success, Issues(composed.Issues));
            var worseningDraft = Assert.Single(composed.Transitions);
            Assert.Equal("worsen", worseningDraft.Kind);
            Assert.Equal("untreated", worseningDraft.ProposedAfter.Care.State);
            Assert.Null(worseningDraft.ProposedAfter.Care.StabilizedAtTurn);
            Assert.Contains("not_stabilized", worseningDraft.ProposedAfter.Recovery.Blockers);
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            var distributor = new StateDistributor(
                FileSystem,
                NullLogger<StateDistributor>.Instance);
            distributor.DistributeAsync(response, composed).GetAwaiter().GetResult();
            var validationIssues = new ValidationService(
                    FileSystem,
                    NullLogger<ValidationService>.Instance)
                .ValidateAcceptedTurnRawEffectMaterializationAsync()
                .GetAwaiter().GetResult();
            Assert.DoesNotContain(
                validationIssues,
                static issue => issue.Severity == IssueSeverity.Error);
            Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
            var published = Assert.IsType<AcceptedMechanicsPlan>(
                new CanonicalStateNormalizer(
                        FileSystem,
                        NullLogger<CanonicalStateNormalizer>.Instance)
                    .BindTo(Lease)
                    .NormalizeAcceptedMechanicsAsync(backups: null)
                    .GetAwaiter().GetResult());
            var finalized = Assert.IsType<AcceptedMechanicsWoundStageBundle>(
                published.WoundStageBundle).FinalPlan;
            var worseningTransitionId = Assert.Single(finalized.AllocatedTransitionIds);
            Binding = ExportRecoveryBinding(FileSystem, Lease, WoundId);
            var history = WoundHistoryState.Parse(
                File.ReadAllText(FileSystem.ResolvePath(WoundHistoryState.HistoryPath)),
                WoundHistoryState.HistoryPath);
            Assert.True(history.IsValid, Issues(history.Issues));
            var publishedTransition = Assert.Single(
                history.State!.Transitions,
                value => string.Equals(value.TransitionId, worseningTransitionId, StringComparison.Ordinal));
            Assert.Equal("worsen", publishedTransition.Kind);
            Assert.Equal(turn, publishedTransition.Turn);
            return new WorseningReentryPublication(worseningTransitionId);
        }

        internal void TamperPersistedRecoveryEvidence(string kind)
        {
            var history = ReadRoot(FileSystem, WoundHistoryState.HistoryPath);
            var transitions = Assert.IsType<JsonArray>(history["transitions"]);
            Assert.NotEmpty(transitions);
            switch (kind)
            {
                case "history":
                    Assert.IsType<JsonObject>(transitions[^1])["afterFingerprint"] =
                        "sha256:" + new string('0', 64);
                    break;
                case "receipt":
                    var fingerprints = FindProperties(history, "receiptFingerprint").ToArray();
                    var receipt = Assert.Single(fingerprints);
                    receipt["receiptFingerprint"] = "sha256:" + new string('0', 64);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
            Write(FileSystem, WoundHistoryState.HistoryPath, history);
        }

        public void Dispose() { Lease.DisposeAsync().AsTask().GetAwaiter().GetResult(); Directory.Delete(Root, recursive: true); }

        private static void ComposeAndPublishWoundStages(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsWoundStageBundle bundle)
        {
            var wholeTreeBefore = CaptureTreeBytes(fs.GameSessionPath);
            var method = ExactStatic(typeof(AcceptedMechanicsPlanAuthority), "GetOrBuildWoundValidated", 3);
            Assert.Equal(typeof(AcceptedMechanicsPlanningResult), method.ReturnType);
            Assert.Equal(typeof(FileSystemManager), method.GetParameters()[0].ParameterType);
            Assert.Equal(lease.GetType(), method.GetParameters()[1].ParameterType);
            Assert.Equal(typeof(AcceptedMechanicsWoundStageBundle), method.GetParameters()[2].ParameterType);
            var result = Invoke(method, fs, lease, bundle);
            AssertClosed(result, "Issues", "Plan", "Success");
            var resultIssues = Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
                Required(result, "Issues")).ToArray();
            Assert.True(
                Assert.IsType<bool>(Required(result, "Success")),
                Issues(resultIssues));
            Assert.Empty(resultIssues);
            var plan = Assert.IsType<AcceptedMechanicsPlan>(Required(result, "Plan"));
            AssertPublishedWoundPlan(plan, bundle, Assert.Single(bundle.FinalPlan.AllocatedWoundIds));
            AssertTreeBytesUnchanged(fs, wholeTreeBefore);
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
            var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                ExportAcceptedState(fs, lease, woundId));
            var binding = acceptedState.Binding;
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
            var governedBefore = CaptureAllGovernedBytes(fs);
            var wholeTreeBefore = CaptureTreeBytes(fs.GameSessionPath);
            var composed = Invoke(compose, fs, lease, new GameResponse(), acceptedState, request, resolution);
            var plan = Assert.IsType<AcceptedMechanicsPlan>(Required(composed, "Plan"));
            AssertPublishedWoundPlan(
                plan,
                Assert.IsType<AcceptedMechanicsWoundStageBundle>(plan.WoundStageBundle),
                woundId);
            AssertAllGovernedBytesUnchanged(fs, governedBefore);
            AssertTreeBytesUnchanged(fs, wholeTreeBefore);
            Assert.Same(plan, new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAcceptedMechanicsAsync(null).GetAwaiter().GetResult());
            Assert.NotEqual("untreated", ReadCanonicalWound(fs, woundId).Care.State);
        }

        internal static IReadOnlyDictionary<string, byte[]?> CaptureAllGovernedBytes(
            FileSystemManager fs,
            AcceptedMechanicsPlan? plan = null)
        {
            var paths = new HashSet<string>(WoundAcceptedTurnSnapshotContract.RequiredPaths,
                StringComparer.Ordinal)
            {
                WoundCarrierCatalog.PlayerPath,
                WoundIdentityState.StatePath,
                WoundHistoryState.HistoryPath,
                EffectAcceptedTurnInputComposer.WorldTimePath,
                EffectAcceptedTurnPlan.IdentityIndexPath,
                AcceptedMechanicsPlan.DefinitionPath,
                AcceptedMechanicsPlan.StatePath,
                AcceptedMechanicsPlan.HistoryPath,
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath
            };
            if (plan is not null)
            {
                paths.UnionWith(plan.TouchedPaths);
                paths.UnionWith(plan.ConsumedPaths);
                paths.UnionWith(plan.BeforeImages.Keys);
                paths.UnionWith(plan.WoundCarrierAfterImages.Keys);
                paths.UnionWith(plan.EffectCarrierAfterImages.Keys);
                paths.UnionWith(plan.OwnerCompanionAfterImages.Keys);
                paths.UnionWith(plan.PendingAfterImages.Keys);
            }
            return paths.ToDictionary(path => path, path => ReadBytes(fs, path), StringComparer.Ordinal);
        }

        internal static void AssertAllGovernedBytesUnchanged(
            FileSystemManager fs,
            IReadOnlyDictionary<string, byte[]?> before)
        {
            foreach (var pair in before)
            {
                AssertBytesEqual(pair.Value, ReadBytes(fs, pair.Key), pair.Key);
            }
        }

        internal static void AssertPlanBeforeImagesMatchFilesystem(
            FileSystemManager fs,
            AcceptedMechanicsPlan plan)
        {
            foreach (var pair in plan.BeforeImages)
            {
                var actual = ReadBytes(fs, pair.Key);
                Assert.Equal(pair.Value.Existed, actual is not null);
                AssertBytesEqual(pair.Value.Bytes, actual, pair.Key);
            }
        }

        internal static void AssertComposeDidNotWrite(
            FileSystemManager fs,
            IReadOnlyDictionary<string, byte[]?> baseline,
            AcceptedMechanicsPlan plan)
        {
            var governed = CaptureAllGovernedBytes(fs, plan);
            AssertAllGovernedBytesUnchanged(fs, baseline);
            AssertPlanBeforeImagesMatchFilesystem(fs, plan);
            Assert.All(plan.TouchedPaths, path => Assert.Contains(path, governed.Keys));
            Assert.All(plan.ConsumedPaths, path => Assert.Contains(path, governed.Keys));
            Assert.All(plan.BeforeImages.Keys, path => Assert.Contains(path, governed.Keys));
        }

        internal static void AssertAcceptedPlanPublishedExactly(
            FileSystemManager fs,
            IReadOnlyDictionary<string, byte[]?> beforeCompose,
            AcceptedMechanicsPlan plan)
        {
            var writes = ExpectedPublishedAfterImages(plan);
            var deletes = plan.PendingAfterImages
                .Where(static pair => pair.Value is null)
                .Select(static pair => pair.Key)
                .Concat(plan.ConsumedPaths)
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var pair in writes)
            {
                var bytes = ReadBytes(fs, pair.Key);
                Assert.NotNull(bytes);
                var actual = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(bytes!));
                Assert.True(JsonNode.DeepEquals(pair.Value, actual), pair.Key);
            }
            foreach (var path in deletes)
                Assert.Null(ReadBytes(fs, path));

            var produced = writes.Keys.Concat(deletes).ToHashSet(StringComparer.Ordinal);
            foreach (var pair in plan.BeforeImages.Where(pair => !produced.Contains(pair.Key)))
            {
                var actual = ReadBytes(fs, pair.Key);
                Assert.Equal(pair.Value.Existed, actual is not null);
                AssertBytesEqual(pair.Value.Bytes, actual, pair.Key);
            }
            foreach (var pair in beforeCompose.Where(pair => !produced.Contains(pair.Key)))
                AssertBytesEqual(pair.Value, ReadBytes(fs, pair.Key), pair.Key);
        }

        private static IReadOnlyDictionary<string, JsonObject> ExpectedPublishedAfterImages(
            AcceptedMechanicsPlan plan)
        {
            Assert.Empty(plan.OwnerTransitions);
            var writes = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            AddWrite(writes, AcceptedMechanicsPlan.DefinitionPath, plan.DefinitionAfterImage);
            AddWrite(writes, AcceptedMechanicsPlan.StatePath, plan.StateAfterImage);
            AddWrite(writes, AcceptedMechanicsPlan.HistoryPath, plan.HistoryAfterImage);
            AddWrite(writes, CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                ExpectedCanonicalResourceOwnerAuthorityAfterImage(plan));
            AddWrite(writes, EffectAcceptedTurnPlan.IdentityIndexPath, plan.EffectIdentityAfterImage);
            foreach (var pair in plan.OwnerCompanionAfterImages)
                AddWrite(writes, pair.Key, pair.Value);
            foreach (var pair in plan.EffectCarrierAfterImages)
                AddWrite(writes, pair.Key, pair.Value);
            foreach (var pair in plan.WoundCarrierAfterImages)
                AddWrite(writes, pair.Key, pair.Value);
            if (plan.WoundIdentityAfterImage is { } identity)
                AddWrite(writes, WoundIdentityState.StatePath, identity);
            if (plan.WoundHistoryAfterImage is { } history)
                AddWrite(writes, WoundHistoryState.HistoryPath, history);
            foreach (var pair in plan.PendingAfterImages.Where(static pair => pair.Value is not null))
                AddWrite(writes, pair.Key, pair.Value!);
            return writes;
        }

        private static JsonObject ExpectedCanonicalResourceOwnerAuthorityAfterImage(
            AcceptedMechanicsPlan plan)
        {
            var definitions = ResourceDefinitionCatalog.ParseCanonical(
                plan.DefinitionAfterImage.ToJsonString(), allowMissingPristine: false);
            Assert.NotNull(definitions.Catalog);
            var state = ResourceStateContract.ParseCanonical(
                plan.StateAfterImage.ToJsonString(), definitions.Catalog!, allowMissingPristine: false);
            var history = ResourceHistoryState.ParseCanonical(
                plan.HistoryAfterImage.ToJsonString(), definitions.Catalog!, allowMissingPristine: false);
            Assert.NotNull(state.Ledger);
            Assert.NotNull(history.History);
            return JsonNode.Parse(CanonicalResourceOwnerAuthorityComposer.CreateCanonicalAuthorityJson(
                plan.OwnerAuthority, state.Ledger!, history.History!))!.AsObject();
        }

        private static void AddWrite(
            IDictionary<string, JsonObject> writes,
            string path,
            JsonObject value)
        {
            if (writes.TryGetValue(path, out var previous))
            {
                Assert.True(JsonNode.DeepEquals(previous, value), path);
                return;
            }
            writes.Add(path, value);
        }

        private static byte[]? ReadBytes(FileSystemManager fs, string path)
        {
            var full = fs.ResolvePath(path);
            return File.Exists(full) ? File.ReadAllBytes(full) : null;
        }

        private static IReadOnlyDictionary<string, byte[]> CaptureTreeBytes(string root) =>
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => new
                {
                    FullPath = path,
                    RelativePath = Path.GetRelativePath(root, path).Replace('\\', '/')
                })
                .Where(static file => !file.RelativePath.StartsWith(
                    ".boe_runtime/",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
                .ToDictionary(
                    static file => file.RelativePath,
                    static file => File.ReadAllBytes(file.FullPath),
                    StringComparer.Ordinal);

        private static void AssertTreeBytesUnchanged(
            FileSystemManager fs,
            IReadOnlyDictionary<string, byte[]> before)
        {
            var after = CaptureTreeBytes(fs.GameSessionPath);
            Assert.Equal(before.Keys.OrderBy(static path => path, StringComparer.Ordinal),
                after.Keys.OrderBy(static path => path, StringComparer.Ordinal));
            foreach (var pair in before)
            {
                Assert.True(after.TryGetValue(pair.Key, out var actual), pair.Key);
                Assert.True(pair.Value.AsSpan().SequenceEqual(actual), pair.Key);
            }
        }

        private static void AssertBytesEqual(byte[]? expected, byte[]? actual, string path)
        {
            Assert.Equal(expected is not null, actual is not null);
            if (expected is not null)
                Assert.Equal(expected, actual);
        }

        internal const string CanonicalDeteriorationPolicyPath =
            "game_state/player/wounds.json.activeWounds[0].recovery.deteriorationPolicy";

        private static WoundAcceptedTurnInput PrepareSignedCreationTurn(
            FileSystemManager fs,
            WoundAcceptedTurnInput input)
        {
            var binding = input.Binding;
            _ = new LiveTurnPreparationService(fs).PrepareAsync(
                new LiveTurnPreparationOptions
                {
                    SessionId = binding.SessionId,
                    RequestId = binding.RequestId,
                    TurnNumber = binding.Turn,
                    CurrentRealm = "Mortal World",
                    PlayerAction = "T062 initial wound creation",
                    PreGeneratedDices1d20 = new[] { 17 }
                }).GetAwaiter().GetResult();
            var manifest = JsonNode.Parse(File.ReadAllText(fs.ResolvePath(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath)))!
                .AsObject();
            var snapshotToken = manifest["manifestPayloadHash"]!.GetValue<string>();
            return input with
            {
                Binding = binding with { SnapshotToken = snapshotToken },
                Opportunities = input.Opportunities.Select(value =>
                {
                    var rebound = value with { SnapshotToken = snapshotToken };
                    return rebound with
                    {
                        AuthorityFingerprint =
                            WoundOpportunityAuthority
                                .RecomputeAuthorityFingerprint(rebound)
                    };
                }).ToArray()
            };
        }

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
            var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                ExportAcceptedState(fs, lease, woundId));
            return acceptedState.Binding;
        }

        private static object ExportAcceptedState(
            FileSystemManager fs,
            FileSystemManager.CanonicalWriteLease lease,
            string woundId)
        {
            var result = ExportAcceptedStateResult(fs, lease, woundId);
            AssertClosed(result, "Authority", "IsValid", "Issues");
            Assert.True(Assert.IsType<bool>(Required(result, "IsValid")), Issues(Values(result, "Issues").Select(Assert.IsType<ValidationIssue>)));
            return Required(result, "Authority");
        }

        private static object ExportAcceptedStateResult(
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
            return Invoke(export, fs, lease, context, woundId);
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

        private static JsonObject CreateWorseningReentryProposal(
            WoundMaterializationEnvelope before,
            string narration)
        {
            var canonical = JsonNode.Parse(
                WoundMaterializationContract.SerializeCanonical(before))!.AsObject();
            var classification = canonical["classification"]!.DeepClone().AsObject();
            classification.Remove("domain");
            var display = canonical["display"]!.DeepClone().AsObject();
            display["description"] =
                "Повторная травма раскрыла рану и вернула опасность осложнений.";
            display["visibleSymptoms"] = new JsonArray(
                "возобновившееся кровотечение",
                "резкая боль при движении");
            display["prognosis"] =
                "Рану необходимо снова стабилизировать до продолжения восстановления.";
            display["acquisitionNarration"] = narration;
            var recovery = canonical["recovery"]!.DeepClone().AsObject();
            recovery.Remove("recoveryAnchor");
            recovery.Remove("deteriorationAnchor");
            recovery["currentStepProgress"] = 0;
            recovery["blockers"] = new JsonArray("not_stabilized");
            return new JsonObject
            {
                ["classification"] = classification,
                ["display"] = display,
                ["severity"] = "III",
                ["complications"] = new JsonArray(),
                ["consequenceDefinitions"] = new JsonArray(),
                ["treatment"] = canonical["treatment"]!.DeepClone(),
                ["recovery"] = recovery
            };
        }

        private static JsonObject CreateWorseningReentrySource(
            WoundMaterializationEnvelope before,
            string opportunityRef,
            int turn) => new()
        {
            ["schemaVersion"] = 1,
            ["adapterKind"] = "formal",
            ["acceptedEventOrdinal"] = 0,
            ["opportunityRef"] = opportunityRef,
            ["owner"] = new JsonObject
            {
                ["realm"] = before.Owner.Realm,
                ["ownerKind"] = before.Owner.OwnerKind,
                ["ownerId"] = before.Owner.OwnerId,
                ["carrierPath"] = before.Owner.CarrierPath
            },
            ["domain"] = before.Classification.Domain,
            ["profileKey"] = "mortal_formal_injury_v1",
            ["source"] = new JsonObject
            {
                ["kind"] = "combat_action",
                ["sourceId"] = $"combat_retrauma_t062_{turn}",
                ["state"] = "active"
            },
            ["outcome"] = new JsonObject
            {
                ["kind"] = "harmful",
                ["maximumSeverityRank"] = 3,
                ["readableCause"] =
                    "Повторный удар снова раскрыл стабилизированную рану."
            },
            ["safeContext"] = new JsonObject
            {
                ["target"] = "вы",
                ["cause"] = "повторный удар",
                ["allowedLocationKinds"] = new JsonArray(
                    "anatomical",
                    "systemic",
                    "other")
            },
            ["worseningTarget"] = new JsonObject
            {
                ["woundId"] = before.WoundId,
                ["causeKind"] = "retrauma"
            }
        };

        private static WoundResponseInputCompositionResult ComposeAcceptedWorseningResponse(
            FileSystemManager fs,
            FileSystemManager.CanonicalWriteLease lease,
            JsonObject sourceEvent,
            GameResponse response)
        {
            var adapter = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.MortalWoundOpportunityAdapter",
                throwOnError: false,
                ignoreCase: false);
            Assert.NotNull(adapter);
            Assert.Empty(adapter!.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
            var compose = ExactStatic(adapter, "ComposeAcceptedResponse", 4);
            var parameters = compose.GetParameters();
            Assert.Equal(typeof(FileSystemManager), parameters[0].ParameterType);
            Assert.Equal(lease.GetType(), parameters[1].ParameterType);
            Assert.Equal(typeof(JsonElement), parameters[2].ParameterType);
            Assert.Equal(typeof(GameResponse), parameters[3].ParameterType);
            Assert.DoesNotContain(
                adapter.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                static method => method.Name == "ComposeAcceptedResponse" &&
                                 method.GetParameters().Any(parameter =>
                                     parameter.ParameterType == typeof(WoundAcceptedTurnBinding) ||
                                     parameter.ParameterType == typeof(WoundOpportunityAuthority) ||
                                     parameter.Name?.Contains("fingerprint", StringComparison.OrdinalIgnoreCase) == true));
            var composed = Invoke(
                compose,
                fs,
                lease,
                JsonSerializer.SerializeToElement(sourceEvent),
                response);
            return Assert.IsType<WoundResponseInputCompositionResult>(composed);
        }

        private static WoundCarrierCatalogInput ReadWoundCarriers(FileSystemManager fs) => new(
            ReadOptionalRoot(fs, WoundCarrierCatalog.PlayerPath),
            ReadOptionalRoot(fs, WoundCarrierCatalog.NpcPath),
            ReadOptionalRoot(fs, WoundCarrierCatalog.EnemiesPath),
            ReadOptionalRoot(fs, WoundCarrierCatalog.AlliesPath),
            ReadOptionalRoot(fs, WoundCarrierCatalog.AfterlifeProfilesPath));

        private static EffectCarrierCatalogInput ReadEffectCarriers(FileSystemManager fs) => new(
            ReadOptionalRoot(fs, EffectCarrierCatalog.PlayerPath),
            ReadOptionalRoot(fs, EffectCarrierCatalog.NpcPath),
            ReadOptionalRoot(fs, EffectCarrierCatalog.EnemiesPath),
            ReadOptionalRoot(fs, EffectCarrierCatalog.AlliesPath),
            ReadOptionalRoot(fs, EffectCarrierCatalog.AfterlifeProfilesPath),
            ReadOptionalRoot(fs, EffectCarrierCatalog.SpiritualConflictPath));

        private static JsonObject? ReadOptionalRoot(FileSystemManager fs, string path) =>
            ReadOptionalNode(fs, path) as JsonObject;

        private static JsonNode? ReadOptionalNode(FileSystemManager fs, string path)
        {
            var full = fs.ResolvePath(path);
            return File.Exists(full) ? JsonNode.Parse(File.ReadAllText(full)) : null;
        }

        private static void PrepareLiveTurn(FileSystemManager fs, int turn, string operation) =>
            new LiveTurnPreparationService(fs).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = "session_t062", RequestId = $"request_t062_{turn}", TurnNumber = turn,
                CurrentRealm = "Mortal World", PlayerAction = "T062 " + operation,
                PreGeneratedDices1d20 = new[] { 17 }
            }).GetAwaiter().GetResult();

        private static void WriteCanonicalTreatmentAuthorityRoots(FileSystemManager fs)
        {
            var sterileThread = MortalItemTestFixture.CreateCanonicalRoot("sterile_thread");
            sterileThread["count"] = 2;
            sterileThread["quality"] = "Rare";
            sterileThread["rarity"] = "Rare";
            MortalItemTestFixture.ResealCanonical(sterileThread);
            Write(fs, "game_state/player/skills_active.json", new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(CreateTreatmentSkill(
                    "skill_player_first_aid_01", "player_first_aid"))
            });
            Write(fs, "game_state/player/skills_passive.json", new JsonObject
            {
                ["passiveSkillChanges"] = new JsonArray()
            });
            Write(fs, "game_state/player/skill_mastery.json", new JsonObject
            {
                ["skillMasteryChanges"] = new JsonArray(new JsonObject
                {
                    ["skillName"] = "Field Medicine",
                    ["newMasteryLevel"] = 3,
                    ["newCurrentMasteryProgress"] = 0,
                    ["newMasteryProgressNeeded"] = 100,
                    ["masteryLeveledUp"] = false
                })
            });
            var medicSkill = CreateTreatmentSkill(
                "skill_field_medicine_01",
                "field_medicine");
            medicSkill["currentMasteryLevel"] = 3;
            var medic = MortalActorTestFixtures.CreateActor(
                "field_medic_01",
                "loc_field_clinic_001",
                "T062 field clinic");
            medic["inventory"] = new JsonArray(sterileThread);
            medic["activeSkills"] = new JsonArray(medicSkill);
            medic["passiveSkills"] = new JsonArray();
            Write(fs, "game_state/npcs/npc_core.json", new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(medic)
            });
            Write(fs, "game_state/inventory/item_identity_index.json",
                MortalItemTestFixture.CreateIndexForCarrier(
                    sterileThread, "npc_inventory", "field_medic_01"));
            var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
                "loc_field_clinic_001",
                "T062 field clinic");
            location["customStates"] = new JsonArray(
                new JsonObject
                {
                    ["kind"] = MortalWoundTreatmentSceneAuthorityContract.FacilityKind,
                    ["schemaVersion"] = 1,
                    ["facilityId"] = "fac_field_clinic_001",
                    ["displayName"] = "Field clinic",
                    ["available"] = true
                },
                new JsonObject
                {
                    ["kind"] = MortalWoundTreatmentSceneAuthorityContract.ConsentKind,
                    ["schemaVersion"] = 1,
                    ["consentRef"] = "consent_field_medic_player_01",
                    ["displayName"] = "Field medic consent",
                    ["providerKind"] = "npc",
                    ["providerId"] = "field_medic_01",
                    ["targetKind"] = "player",
                    ["targetId"] = "player_current",
                    ["status"] = "granted"
                });
            location["materialization"]!["sections"]!["customStates"] = new JsonObject
            {
                ["disposition"] = "populated",
                ["reason"] = null
            };
            MortalLocationTestFixture.ResealCanonicalLocation(location);
            Write(
                fs,
                MortalLocationMaterializationContract.WorldMapPath,
                MortalLocationTestFixture.CreateWorldMap(location));
            Write(
                fs,
                MortalLocationMaterializationContract.CurrentLocationPath,
                MortalLocationTestFixture.CreateCurrentProjection(location));
            Write(
                fs,
                MortalLocationIdentityState.StatePath,
                MortalLocationTestFixture.CreateIdentityIndex(location));
            Write(fs, "game_state/meta/soul_state.json", new JsonObject { ["currentRealm"] = "Mortal World" });
        }

        private static void AssertCanonicalTreatmentAuthorityRoots(FileSystemManager fs)
        {
            var npc = ReadRoot(fs, "game_state/npcs/npc_core.json");
            var medic = Assert.IsType<JsonObject>(Assert.Single(
                npc["NPCsInScene"]!.AsArray()));
            Assert.Equal("field_medic_01", medic["NPCId"]!.GetValue<string>());
            Assert.Equal("loc_field_clinic_001", medic["currentLocationId"]!.GetValue<string>());
            Assert.Equal("sterile_thread", medic["inventory"]![0]!["itemId"]!.GetValue<string>());
            Assert.Equal("skill_field_medicine_01", medic["activeSkills"]![0]!["skillId"]!.GetValue<string>());
            Assert.Equal("field_medicine", medic["activeSkills"]![0]!["mortalWoundTreatmentCapabilities"]![0]!["capabilityRef"]!.GetValue<string>());
            var location = ReadRoot(fs, MortalLocationMaterializationContract.CurrentLocationPath);
            Assert.Equal("loc_field_clinic_001", location["locationId"]!.GetValue<string>());
            var facility = Assert.Single(
                location["customStates"]!.AsArray().OfType<JsonObject>(),
                static row => string.Equals(
                    row["kind"]?.GetValue<string>(),
                    MortalWoundTreatmentSceneAuthorityContract.FacilityKind,
                    StringComparison.Ordinal));
            Assert.Equal("fac_field_clinic_001", facility["facilityId"]!.GetValue<string>());
        }

        private static JsonObject CreateTreatmentSkill(string skillId, string capabilityRef) => new()
        {
            ["skillId"] = skillId, ["displayName"] = "Field Medicine", ["lifecycle"] = "active",
            ["active"] = true, ["tier"] = 3, ["skillName"] = "Field Medicine",
            ["skillDescription"] = "Provides precise field care under pressure.", ["rarity"] = "Common",
            ["actionCost"] = "Main",
            ["combatEffect"] = new JsonObject
            {
                ["isActivatedEffect"] = true, ["actionName"] = "Field treatment",
                ["effects"] = new JsonArray(new JsonObject
                {
                    ["effectType"] = "Damage", ["value"] = "10%", ["targetType"] = "Enemy",
                    ["effectDescription"] = "A controlled intervention.", ["poiseDamage"] = "5%"
                })
            },
            ["mortalWoundTreatmentCapabilities"] = new JsonArray(new JsonObject
            {
                ["schemaVersion"] = 1, ["capabilityRef"] = capabilityRef,
                ["woundDomain"] = "physical",
                ["minimumSeverityRank"] = 1, ["maximumSeverityRank"] = 4,
                ["operationLimits"] = new JsonObject
                {
                    ["mayStabilize"] = true, ["maximumRecoveryPoints"] = 2,
                    ["maximumSeverityReductionSteps"] = 1,
                    ["removableComplicationKinds"] = new JsonArray("infection"),
                    ["mayHealAtSeverityI"] = true, ["maximumCosmeticHealLegacies"] = 1,
                    ["maximumMechanicalEffectHealLegacies"] = 1
                }
            })
        };

        private JsonObject? Read(string path)
        {
            var full = FileSystem.ResolvePath(path);
            return File.Exists(full) ? JsonNode.Parse(File.ReadAllText(full))!.AsObject() : null;
        }

        private static JsonObject ReadRoot(FileSystemManager fs, string path) =>
            JsonNode.Parse(File.ReadAllText(fs.ResolvePath(path)))!.AsObject();

        private static IEnumerable<JsonObject> FindProperties(JsonNode? value, string propertyName)
        {
            switch (value)
            {
                case JsonObject obj:
                    if (obj.ContainsKey(propertyName))
                        yield return obj;
                    foreach (var (_, child) in obj)
                    {
                        foreach (var match in FindProperties(child, propertyName))
                            yield return match;
                    }
                    yield break;
                case JsonArray array:
                    foreach (var child in array)
                    {
                        foreach (var match in FindProperties(child, propertyName))
                            yield return match;
                    }
                    yield break;
                default:
                    yield break;
            }
        }

        private static void Write(FileSystemManager fs, string path, JsonObject root)
        {
            var full = fs.ResolvePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, root.ToJsonString());
        }
    }
}
