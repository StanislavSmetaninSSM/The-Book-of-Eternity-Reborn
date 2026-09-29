using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
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
/// publisher. Empty non-player carrier foundations are seeded before the signed target
/// scan; tests never write a materialized wound, identity/history row, consumed receipt,
/// or canonical wound after-image.
/// T069 interruption classification is separately
/// MortalWoundDeteriorationPolicyAuthority.Create(FileSystemManager, CanonicalWriteLease,
/// WoundAcceptedTurnBinding, string woundId, string policyRef).
/// </summary>
[Trait("Category", "RegressionIntegration")]
public sealed partial class MortalWoundRecoveryTests
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

    /// <summary>
    /// Shares genuine source publication across clocks while signing each distinct
    /// evaluation turn and retaining independent authorities and exact source bytes.
    /// </summary>
    [Fact]
    public void FixtureSourceCache_ReusesPublishedSourceAcrossGenuinelySignedEvaluationClocks()
    {
        var baseline = Scenario.MultiCadenceJump();
        var authored = baseline.Wound.DeepClone().AsObject();
        authored["display"]!["name"] = "recovery_source_cache_contract_" + Guid.NewGuid().ToString("N");
        baseline = baseline with { Wound = authored };
        var firstScenario = baseline with { Minute = 109 };
        var secondScenario = baseline with { Minute = 110 };
        var thirdScenario = baseline with { Minute = 135 };
        var first = Fixture.Create(firstScenario);
        Fixture? firstToDispose = first;
        try
        {
            using var second = Fixture.Create(secondScenario);
            Assert.Equal(1, Fixture.GetSourcePreparationCount(firstScenario));
            Assert.Equal(1, Fixture.GetSourcePreparationCount(secondScenario));
            Assert.Equal(1, Fixture.GetPreparationCount(firstScenario));
            Assert.Equal(1, Fixture.GetPreparationCount(secondScenario));
            Assert.NotEqual(first.FileSystem.BasePath, second.FileSystem.BasePath);
            Assert.NotSame(first.FileSystem, second.FileSystem);
            Assert.NotSame(first.Lease, second.Lease);
            Assert.NotSame(first.FileSystem.CanonicalRootAuthorityIdentity,
                second.FileSystem.CanonicalRootAuthorityIdentity);
            var firstAuthority = AssertRecoveryFixtureClock(first, firstScenario.Minute);
            var secondAuthority = AssertRecoveryFixtureClock(second, secondScenario.Minute);
            Assert.NotSame(firstAuthority, secondAuthority);
            Assert.NotEqual(firstAuthority.SessionGeneration, secondAuthority.SessionGeneration);
            Assert.NotEqual(firstAuthority.AcceptedStateFingerprint, secondAuthority.AcceptedStateFingerprint);
            Assert.False(firstAuthority.IsLeaseBoundTo(second.FileSystem, second.Lease));
            Assert.False(secondAuthority.IsLeaseBoundTo(first.FileSystem, first.Lease));
            var sourceHistory = File.ReadAllBytes(second.FileSystem.ResolvePath(WoundHistoryState.HistoryPath));
            var sourceIdentity = File.ReadAllBytes(second.FileSystem.ResolvePath(WoundIdentityState.StatePath));
            var sourceRecoveryAnchor = Assert.IsType<byte[]>(second.CaptureRecoveryAnchorBytes());
            Assert.Equal(100, JsonNode.Parse(sourceRecoveryAnchor)!["anchorMinute"]!.GetValue<long>());
            Assert.Equal(sourceHistory, File.ReadAllBytes(first.FileSystem.ResolvePath(WoundHistoryState.HistoryPath)));
            Assert.Equal(sourceIdentity, File.ReadAllBytes(first.FileSystem.ResolvePath(WoundIdentityState.StatePath)));
            Assert.Equal(sourceRecoveryAnchor, first.CaptureRecoveryAnchorBytes());
            Assert.Null(first.InitialDeteriorationAnchorBytes);
            Assert.Null(first.PostStabilizationDeteriorationAnchorBytes);
            Assert.Equal(first.InitialDeteriorationAnchorBytes, second.InitialDeteriorationAnchorBytes);
            Assert.Equal(first.PostStabilizationDeteriorationAnchorBytes, second.PostStabilizationDeteriorationAnchorBytes);
            var history = WoundHistoryState.Parse(File.ReadAllText(second.FileSystem.ResolvePath(WoundHistoryState.HistoryPath)),
                WoundHistoryState.HistoryPath);
            Assert.True(history.IsValid, Issues(history.Issues));
            Assert.Equal(new[] { "create", "treat" }, history.State!.Transitions.Select(row => row.Kind));
            var snapshotPath = LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/" +
                WoundCarrierCatalog.PlayerPath;
            var originalSnapshot = File.ReadAllBytes(second.FileSystem.ResolvePath(snapshotPath));
            File.WriteAllBytes(first.FileSystem.ResolvePath(snapshotPath), System.Text.Encoding.UTF8.GetBytes("{}"));
            var rejected = first.ExportCurrentResult();
            Assert.False(Assert.IsType<bool>(Required(rejected, "IsValid")));
            Assert.Null(Optional(rejected, "Authority"));
            Assert.NotEmpty(Values(rejected, "Issues"));
            Assert.Equal(originalSnapshot, File.ReadAllBytes(second.FileSystem.ResolvePath(snapshotPath)));
            AssertRecoveryFixtureClock(second, secondScenario.Minute);
            var retiredRoot = first.FileSystem.BasePath;
            first.Dispose();
            firstToDispose = null;
            Assert.False(Directory.Exists(retiredRoot));
            Assert.False(firstAuthority.HasCurrentAdmissionAuthority());

            using var third = Fixture.Create(thirdScenario);
            var thirdAuthority = AssertRecoveryFixtureClock(third, thirdScenario.Minute);
            Assert.Equal(1, Fixture.GetSourcePreparationCount(thirdScenario));
            Assert.Equal(1, Fixture.GetPreparationCount(thirdScenario));
            Assert.NotEqual(second.FileSystem.BasePath, third.FileSystem.BasePath);
            Assert.NotEqual(secondAuthority.SessionGeneration, thirdAuthority.SessionGeneration);
            Assert.NotEqual(firstAuthority.SessionGeneration, thirdAuthority.SessionGeneration);
            Assert.False(thirdAuthority.IsLeaseBoundTo(second.FileSystem, second.Lease));
            Assert.Equal(originalSnapshot, File.ReadAllBytes(third.FileSystem.ResolvePath(snapshotPath)));
            Assert.Equal(sourceHistory, File.ReadAllBytes(third.FileSystem.ResolvePath(WoundHistoryState.HistoryPath)));
            Assert.Equal(sourceIdentity, File.ReadAllBytes(third.FileSystem.ResolvePath(WoundIdentityState.StatePath)));
            Assert.Equal(sourceRecoveryAnchor, third.CaptureRecoveryAnchorBytes());
            Assert.Equal(second.InitialDeteriorationAnchorBytes, third.InitialDeteriorationAnchorBytes);
            Assert.Equal(second.PostStabilizationDeteriorationAnchorBytes, third.PostStabilizationDeteriorationAnchorBytes);
            var measurements = new[] { firstScenario, secondScenario, thirdScenario }
                .Select(Fixture.ReadPreparationMeasurement).ToArray();
            foreach (var measurement in measurements)
                Assert.Equal(100L, Assert.IsType<long>(Required(measurement, "SourceCurrentTimeInMinutes")));
            var diagnosticDirectory = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "fixture-performance");
            Directory.CreateDirectory(diagnosticDirectory);
            File.WriteAllText(Path.Combine(diagnosticDirectory,
                $"recovery-source-{Environment.ProcessId}-{Guid.NewGuid():N}.json"), JsonSerializer.Serialize(new
                {
                    Test = nameof(FixtureSourceCache_ReusesPublishedSourceAcrossGenuinelySignedEvaluationClocks),
                    SourceMeasurementScope = "genuine source construction and byte capture, excluding cleanup",
                    ClockMeasurementScope = "evaluation clock write, real turn44 signing and accepted-state export, excluding source copy",
                    Measurements = measurements
                }));
        }
        finally
        {
            firstToDispose?.Dispose();
        }
    }

    /// <summary>
    /// Authenticates the real signed evaluation clock and its fresh accepted-state owner.
    /// </summary>
    /// <param name="fixture">
    /// Fresh caller-owned fixture whose current files and signed snapshot are compared.
    /// </param>
    /// <param name="minute">
    /// The exact evaluation minute expected in both signed and live canonical world time.
    /// </param>
    /// <returns>
    /// The currently admitted authority bound to this fixture's root and lease.
    /// </returns>
    private static MortalWoundTreatmentAcceptedStateAuthority AssertRecoveryFixtureClock(Fixture fixture, long minute)
    {
        var fs = fixture.FileSystem;
        fixture.AssertCarrierIdentityHistoryAgreement();
        Assert.Equal(minute, EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(
            File.ReadAllText(fs.ResolvePath(EffectAcceptedTurnInputComposer.WorldTimePath))));
        var read = PendingTurnSnapshotReader.ReadCurrent(fs, fixture.Lease,
            [EffectAcceptedTurnInputComposer.WorldTimePath]);
        Assert.True(read.Success, Issues(read.Issues));
        var signed = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
        Assert.Equal(minute, EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(
            System.Text.Encoding.UTF8.GetString(signed.ReadRequiredBytes(EffectAcceptedTurnInputComposer.WorldTimePath))));
        Assert.Equal(44, signed.TurnNumber);
        Assert.Equal(fixture.Binding.SnapshotToken, signed.SnapshotToken);
        var authority = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            Required(fixture.ExportCurrentResult(), "Authority"));
        Assert.Equal(minute, authority.CurrentGameMinute);
        Assert.True(authority.HasCurrentAdmissionAuthority());
        Assert.True(authority.IsLeaseBoundTo(fs, fixture.Lease));
        return authority;
    }

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

    /// <summary>
    /// Verifies that authentic negative treatment evidence prevents request preparation
    /// or accepted-state export before any common-plan composition can occur.
    /// </summary>
    /// <param name="mutation">
    /// The exact provider, skill, item, location, or consent fault to capture in a fresh signed turn.
    /// </param>
    [Theory]
    [InlineData("missing_provider")]
    [InlineData("stale_skill")]
    [InlineData("missing_item")]
    [InlineData("mismatched_location")]
    [InlineData("withdrawn_consent")]
    public void AcceptedStateExport_RejectsCanonicalTreatmentAuthorityFaultBeforeComposition(
        string mutation)
    {
        using var fixture = Fixture.Create(Scenario.RequiresStabilization());
        fixture.AssertPersistedTreatmentRequirements();
        var positiveBefore = fixture.CaptureCanonicalTreeBytes();
        var positiveExport = fixture.ExportCurrentResult();
        AssertClosed(positiveExport, "Authority", "IsValid", "Issues");
        Assert.True(Assert.IsType<bool>(Required(positiveExport, "IsValid")),
            Issues(Values(positiveExport, "Issues").Select(Assert.IsType<ValidationIssue>)));
        Assert.Empty(Values(positiveExport, "Issues"));
        var positiveAuthority = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            Required(positiveExport, "Authority"));
        var positiveRequest = fixture.PrepareCurrentProcedureRequest(
            positiveAuthority, "operation_t062_admission_positive");
        Assert.True(positiveRequest.IsValid, Issues(positiveRequest.Issues));
        Assert.Empty(positiveRequest.Issues);
        var preparedPositive = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            positiveRequest.Request);
        Assert.True(preparedPositive.RollbackNewProvisionalClaims(positiveAuthority));
        fixture.AssertCanonicalTreeBytesUnchanged(positiveBefore);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem, fixture.Lease, out _, out _));

        fixture.PrepareSignedTreatmentAuthorityFault(mutation);
        fixture.AssertPersistedTreatmentRequirements();
        var before = fixture.CaptureCanonicalTreeBytes();
        try
        {
            var exported = fixture.ExportCurrentResult();
            AssertClosed(exported, "Authority", "IsValid", "Issues");
            var exportIssues = Values(exported, "Issues")
                .Select(Assert.IsType<ValidationIssue>).ToArray();
            switch (mutation)
            {
                case "missing_provider":
                case "missing_item":
                case "mismatched_location":
                    Assert.False(Assert.IsType<bool>(Required(exported, "IsValid")),
                        "A structurally inconsistent signed source must not export an authority.");
                    Assert.Null(Optional(exported, "Authority"));
                    var expected = mutation switch
                    {
                        "missing_provider" => (
                            Code: "mortal_wound_treatment_accepted_state_consent_invalid",
                            Path: MortalLocationMaterializationContract.CurrentLocationPath + ".customStates",
                            Expected: "exact co-present consent actors",
                            Actual: "consent_field_medic_player_01"),
                        "missing_item" => (
                            Code: "mortal_wound_treatment_accepted_state_item_identity_mismatch",
                            Path: "game_state/inventory/item_identity_index.json",
                            Expected: "one exact current occurrence for every active identity",
                            Actual: "sterile_thread: state=active; occurrences=0"),
                        "mismatched_location" => (
                            Code: "mortal_wound_treatment_accepted_state_presence_invalid",
                            Path: "game_state/npcs/npc_core.json",
                            Expected: "loc_field_clinic_001",
                            Actual: "loc_elsewhere_001"),
                        _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
                    };
                    Assert.Contains(exportIssues, issue =>
                        issue.Code == expected.Code && issue.FilePath == expected.Path &&
                        issue.Expected == expected.Expected && issue.Actual == expected.Actual);
                    break;
                case "stale_skill":
                case "withdrawn_consent":
                    Assert.True(Assert.IsType<bool>(Required(exported, "IsValid")),
                        Issues(exportIssues));
                    Assert.Empty(exportIssues);
                    var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                        Required(exported, "Authority"));
                    var preparation = fixture.PrepareCurrentProcedureRequest(
                        acceptedState, "operation_t062_admission_negative");
                    try
                    {
                        Assert.False(preparation.IsValid,
                            $"Expected no request for {mutation}; actual IsValid={preparation.IsValid}; " +
                            $"Request={preparation.Request?.RequestFingerprint ?? "null"}; {Issues(preparation.Issues)}");
                        Assert.Null(preparation.Request);
                        var route = Assert.Single(acceptedState.CurrentWound.Treatment.Routes);
                        var requiredKind = mutation == "stale_skill" ? "skill_tier" : "consent";
                        var requirementIndex = Assert.Single(route.Requirements
                            .Select((requirement, index) => (Requirement: requirement, Index: index)),
                            row => row.Requirement.GetProperty("kind").GetString() == requiredKind).Index;
                        var requirementsPath = string.IsNullOrEmpty(route.SourcePath)
                            ? "wound.treatment.routes[0].requirements"
                            : route.SourcePath + ".requirements";
                        var expectedCode = mutation == "stale_skill"
                            ? "mortal_wound_requirement_skill_inactive"
                            : "mortal_wound_requirement_consent_missing";
                        var expectedRequirement = mutation == "stale_skill"
                            ? "active exact skill authority"
                            : "active granted current consent";
                        var actualRequirement = mutation == "stale_skill" ? "inactive" : "withdrawn";
                        Assert.Contains(preparation.Issues, issue =>
                            issue.Code == expectedCode &&
                            issue.FilePath == $"{requirementsPath}[{requirementIndex}]" &&
                            issue.Expected == expectedRequirement && issue.Actual == actualRequirement);
                    }
                    finally
                    {
                        if (preparation.Request is { } unexpectedRequest)
                            Assert.True(unexpectedRequest.RollbackNewProvisionalClaims(acceptedState));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }
        }
        finally
        {
            fixture.AssertCanonicalTreeBytesUnchanged(before);
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
                fixture.FileSystem, fixture.Lease, out _, out _));
        }
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
        var compositionIssues = Values(result, "Issues").Select(Assert.IsType<ValidationIssue>).ToArray();
        Assert.True(Convert.ToString(Required(result, "Disposition")) == "Composed",
            "Recovery must compose successfully. " + string.Join("; ", compositionIssues.Select(
                static issue => $"{issue.Code}@{issue.FilePath}: {issue.Message}; actual={issue.Actual}")));
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
        var completedEffects = Assert.IsType<EffectAcceptedTurnPlan>(acceptedPlan.EffectPlan);
        Assert.True(completedEffects.IsAcceptedBoundaryComplete);
        Assert.Equal(WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(bundle.EffectBatchPlan.EffectPlan),
            completedEffects.AcceptedBoundaryBasePlanFingerprint);
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
        carriers.TryResolveOne(selectedWoundId, out var occurrence);
        var identity = WoundIdentityState.Parse(
            plan.WoundIdentityAfterImage!.ToJsonString(), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(
            plan.WoundHistoryAfterImage!.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(identity.IsValid, Issues(identity.Issues));
        Assert.True(history.IsValid, Issues(history.Issues));
        Assert.True(identity.State!.TryGetEntry(selectedWoundId, out var entry));
        AssertSelectedWoundState(carriers, occurrence, entry!, history.State!, "recoveryPlan");
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
        /// <summary>
        /// Gets whether genuine creation should publish an additional action-control
        /// root to exercise rejection when a later severity budget cannot retain both roots.
        /// </summary>
        internal bool IncludeSecondCreationRoot { get; init; }

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
            "NotDue", null, 160, null, [], stabilizationMinute: 150, anchor: 150, elapsedCadences: 0);
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
        /// <summary>
        /// Creates the anchor lifecycle case with an independent recovery blocker that
        /// keeps its effect-free wound actionable after sealed stabilization.
        /// </summary>
        /// <returns>
        /// A scenario whose deterioration condition is cleared by stabilization while
        /// contamination continues to block recovery.
        /// </returns>
        internal static Scenario DeteriorationAnchorLifecycle()
        {
            var scenario = Create(
                "deterioration_anchor_clear_and_reentry", "requires_stabilization", 160,
                "NotDue", null, 110, 130, [], "increase_severity", "untreated_infection",
                stabilized: true, stabilizationMinute: 150, anchor: 150, noMechanics: true);
            scenario.Wound["recovery"]!["blockers"]!.AsArray().Add("contaminated");
            return scenario;
        }
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

    private sealed partial class Fixture : IDisposable
    {
        private static readonly ConcurrentDictionary<string, Lazy<PreparedRecoverySource>> PreparedRecoverySources =
            new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, int> SourcePreparationCounts = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, double> ClockFinalizationMilliseconds = new(StringComparer.Ordinal);

        /// <summary>
        /// Stores detached publication bytes before the evaluation clock is written.
        /// </summary>
        /// <param name="Tree">
        /// Genuine post-creation or post-stabilization session files, excluding runtime authority.
        /// </param>
        /// <param name="WoundId">
        /// Wound identity allocated by the genuine creation publisher.
        /// </param>
        /// <param name="InitialDeteriorationAnchorBytes">
        /// Private creation-anchor comparison bytes, or <see langword="null"/> when that epoch is absent.
        /// </param>
        /// <param name="PostStabilizationDeteriorationAnchorBytes">
        /// Private settled-anchor comparison bytes, or <see langword="null"/> when no condition epoch remains.
        /// </param>
        /// <param name="PreparationMilliseconds">
        /// Actual source construction and byte-capture duration, excluding source cleanup.
        /// </param>
        /// <param name="SourceCurrentTimeInMinutes">
        /// Actual canonical world minute retained before any evaluation-clock write.
        /// </param>
        private sealed record PreparedRecoverySource(PreparedFixtureTree Tree, string WoundId,
            byte[]? InitialDeteriorationAnchorBytes, byte[]? PostStabilizationDeteriorationAnchorBytes,
            double PreparationMilliseconds, long SourceCurrentTimeInMinutes);

        /// <summary>
        /// Keys every input read before evaluation signing while keeping original world-time bytes exact.
        /// </summary>
        /// <param name="scenario">
        /// Complete authored source setup; evaluation minute and expected outcomes are not source inputs.
        /// </param>
        /// <returns>
        /// A deterministic source key retaining creation, stabilization and source-root distinctions.
        /// </returns>
        private static string GetSourcePreparationKey(Scenario scenario) => WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "mortal_wound_recovery_test_source_preparation", "1", scenario.Wound.ToJsonString(),
            scenario.WorldTime.ToJsonString(), scenario.CreationMinute.ToString(CultureInfo.InvariantCulture),
            scenario.StabilizationMinute?.ToString(CultureInfo.InvariantCulture),
            scenario.StartsStabilized ? "true" : "false", scenario.NoMechanics ? "true" : "false",
            scenario.IncludeSecondCreationRoot ? "true" : "false"
        });

        /// <summary>
        /// Reads the number of genuine upstream factories invoked for an exact source profile.
        /// </summary>
        /// <param name="scenario">
        /// Source setup whose evaluation minute does not affect this counter.
        /// </param>
        /// <returns>
        /// Zero before preparation, otherwise the number of invoked source factories.
        /// </returns>
        internal static int GetSourcePreparationCount(Scenario scenario) =>
            SourcePreparationCounts.TryGetValue(GetSourcePreparationKey(scenario), out var count) ? count : 0;

        /// <summary>
        /// Copies completed source and clock timing scalars without retaining a root or live authority.
        /// </summary>
        /// <param name="scenario">
        /// Exact source and evaluation profile already prepared through <see cref="Create"/>.
        /// </param>
        /// <returns>
        /// Detached diagnostic values for the actual source factory and evaluation signer.
        /// </returns>
        internal static object ReadPreparationMeasurement(Scenario scenario)
        {
            var source = PreparedRecoverySources[GetSourcePreparationKey(scenario)].Value;
            return new
            {
                EvaluationMinute = scenario.Minute,
                SourcePreparations = GetSourcePreparationCount(scenario),
                FullClockPreparations = GetPreparationCount(scenario),
                SourcePreparationMilliseconds = source.PreparationMilliseconds,
                source.SourceCurrentTimeInMinutes,
                ClockFinalizationMilliseconds = ClockFinalizationMilliseconds[GetPreparationKey(scenario)]
            };
        }

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

        /// <summary>
        /// Materializes genuine completed signed preparation into a fresh canonical tree
        /// and exports recovery authority under that fixture's own lease and generation.
        /// </summary>
        /// <param name="scenario">
        /// The recovery conditions, clock boundaries and treatment setup to prepare.
        /// </param>
        /// <param name="hooks">
        /// Optional hooks for the fresh caller-owned filesystem; null uses ordinary I/O.
        /// Hooks are never used to build the shared immutable preparation.
        /// </param>
        /// <returns>
        /// A fixture owning its fresh filesystem, current write lease and recovery binding.
        /// </returns>
        internal static Fixture Create(Scenario scenario, FileSystemManagerHooks? hooks = null) =>
            CreateFromPreparedTemplate(scenario, hooks);

        /// <summary>
        /// Builds the earlier source through genuine creation and optional sealed
        /// stabilization, retaining every original publication and setup assertion.
        /// </summary>
        /// <param name="scenario">
        /// Detached upstream setup; evaluation minute and expected-result fields are not read.
        /// </param>
        /// <param name="key">
        /// Exact source-profile key used only to count genuine preparation attempts.
        /// </param>
        /// <returns>
        /// Portable source bytes and private comparison copies after disposing the hookless source owner.
        /// </returns>
        private static PreparedRecoverySource PrepareRecoverySourceTemplate(Scenario scenario, string key)
        {
            SourcePreparationCounts.AddOrUpdate(key, 1, static (_, count) => checked(count + 1));
            var preparationTimer = Stopwatch.StartNew();
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
                WriteCanonicalResourceAuthority(fs);
                AssertCanonicalTreatmentAuthorityRoots(fs);
                var creationInput = scenario.NoMechanics
                    ? WoundAcceptedTurnTestFixture.CreateNoMechanicsInput()
                    : WoundAcceptedTurnTestFixture.CreateDefaultInput();
                if (parsed.Wound!.Severity.Rank != Assert.Single(creationInput.Transitions).ProposedAfter.Severity.Rank)
                {
                    var rankedDraft = Assert.Single(creationInput.Transitions);
                    creationInput = creationInput with
                    {
                        Opportunities = creationInput.Opportunities.Select(value => value with
                        {
                            MaximumSeverityRank = parsed.Wound.Severity.Rank
                        }).ToArray(),
                        Transitions = new[] { rankedDraft with
                        {
                            ProposedAfter = rankedDraft.ProposedAfter with
                            {
                                Severity = parsed.Wound.Severity with
                                {
                                    LastChangeEventRef = rankedDraft.ProposedAfter.Severity.LastChangeEventRef
                                },
                                Consequences = rankedDraft.ProposedAfter.Consequences with
                                {
                                    SlotBudget = parsed.Wound.Severity.Rank
                                }
                            }
                        }}
                    };
                }
                if (scenario.IncludeSecondCreationRoot)
                {
                    var extraRootDraft = Assert.Single(creationInput.Transitions);
                    var extraDefinition = WoundContractTestData.CreateOwnedEffectDefinition(
                        extraRootDraft.LocalWoundRef, "mortal_world", "definition_recovery_extra_root", "action_control");
                    // The genuine creation assembler adds its exact canonical wound/source link.
                    extraDefinition["links"] = new JsonArray();
                    creationInput = creationInput with
                    {
                        Transitions = new[] { extraRootDraft with
                        {
                            EffectDefinitions = extraRootDraft.EffectDefinitions.Append(
                                new WoundAcceptedEffectDefinitionDraft("draft_recovery_extra_effect", extraDefinition)).ToArray(),
                            RootApplications = extraRootDraft.RootApplications.Append(
                                new WoundAcceptedRootApplicationDraft("draft_recovery_extra_application", "draft_recovery_extra_effect",
                                    "recovery_extra_root_application", WoundRootOwnershipDomain.BaseWound)).ToArray(),
                            SlotBindings = extraRootDraft.SlotBindings.Append(
                                new WoundAcceptedConsequenceSlotBinding(2, "action_control", "draft_recovery_extra_application",
                                    "Movement remains restricted by the wound.")).ToArray()
                        }}
                    };
                }
                Write(fs, EffectAcceptedTurnInputComposer.WorldTimePath, scenario.WorldTime);
                creationInput = PrepareSignedCreationTurn(fs, creationInput);
                AssertLiveTurnCorrelation(fs, creationInput.Binding, creationInput);
                lease = fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
                var creationDraft = Assert.Single(creationInput.Transitions);
                creationInput = creationInput with
                {
                    Transitions = new[] { creationDraft with
                    {
                        ProposedAfter = creationDraft.ProposedAfter with
                        {
                            Recovery = parsed.Wound!.Recovery,
                            Treatment = parsed.Wound.Treatment
                        }
                    }}
                };
                var preparedResult = WoundAcceptedTurnPlanner.Prepare(creationInput);
                Assert.True(preparedResult.Issues.Count == 0, string.Join("; ", preparedResult.Issues.Select(
                    issue => $"{issue.Code}@{issue.FilePath}: {issue.Message}; expected={issue.Expected}; actual={issue.Actual}")));
                var prepared = Assert.IsType<WoundPreparedAcceptedTurnPlan>(preparedResult.Plan);
                var effect = WoundEffectBatchPlanner.Build(prepared,
                    WoundAcceptedTurnTestFixture.CreateEffectInput(prepared),
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
                    ResolveAndPublishStabilization(fs, ref lease, woundId);
                    lease = Assert.IsType<FileSystemManager.CanonicalWriteLease>(lease);
                    if (scenario.NoMechanics)
                    {
                        var stabilized = ReadCanonicalWound(fs, woundId);
                        Assert.Empty(stabilized.Consequences.Entries);
                        Assert.Equal("stabilized", stabilized.Care.State);
                        Assert.DoesNotContain("not_stabilized", stabilized.Recovery.Blockers);
                        Assert.Contains("contaminated", stabilized.Recovery.Blockers);
                    }
                }
                var postStabilizationDeteriorationAnchorBytes =
                    CaptureDeteriorationAnchorBytes(fs);
                var tree = PreparedFixtureTree.Capture(fs.GameSessionPath);
                var sourceMinute = Assert.IsType<long>(EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(
                    File.ReadAllText(fs.ResolvePath(EffectAcceptedTurnInputComposer.WorldTimePath))));
                preparationTimer.Stop();
                return new(tree, woundId, initialDeteriorationAnchorBytes?.ToArray(),
                    postStabilizationDeteriorationAnchorBytes?.ToArray(),
                    preparationTimer.Elapsed.TotalMilliseconds, sourceMinute);
            }
            finally
            {
                if (lease is not null) lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
                DeleteOwnedPreparationRoot(root);
            }
        }

        /// <summary>
        /// Signs a distinct recovery evaluation clock from detached genuine source publication.
        /// </summary>
        /// <param name="scenario">
        /// Complete setup selecting the source profile and the exact evaluation minute to sign.
        /// </param>
        /// <returns>
        /// A temporary fresh owner that must be disposed after its completed signed files are captured.
        /// </returns>
        private static Fixture CreateUncached(Scenario scenario)
        {
            var key = GetSourcePreparationKey(scenario);
            var detached = scenario with
            {
                Wound = scenario.Wound.DeepClone().AsObject(),
                WorldTime = scenario.WorldTime.DeepClone().AsObject(),
                IntentTypes = scenario.IntentTypes.ToArray()
            };
            var source = PreparedRecoverySources.GetOrAdd(key, _ =>
                new Lazy<PreparedRecoverySource>(() => PrepareRecoverySourceTemplate(detached, key),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value;
            var root = Path.Combine(Path.GetTempPath(), "boe-t062-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            FileSystemManager.CanonicalWriteLease? lease = null;
            try
            {
                source.Tree.Materialize(fs.GameSessionPath);
                lease = fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
                var clockTimer = Stopwatch.StartNew();
                Write(fs, EffectAcceptedTurnInputComposer.WorldTimePath,
                    new JsonObject { ["currentTimeInMinutes"] = scenario.Minute });
                lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
                lease = null;
                PrepareLiveTurn(fs, 44, "recovery");
                lease = fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
                // T066 must export the recovery binding from the current prepared turn;
                // the bootstrap creation binding is never reused for this continuation.
                var recoveryBinding = ExportRecoveryBinding(fs, lease, source.WoundId);
                clockTimer.Stop();
                ClockFinalizationMilliseconds[GetPreparationKey(scenario)] = clockTimer.Elapsed.TotalMilliseconds;
                return new(root, fs, lease, recoveryBinding, source.WoundId, scenario,
                    source.InitialDeteriorationAnchorBytes?.ToArray(),
                    source.PostStabilizationDeteriorationAnchorBytes?.ToArray());
            }
            catch
            {
                if (lease is not null) lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
                DeleteOwnedPreparationRoot(root);
                throw;
            }
        }

        /// <summary>
        /// Removes only the isolated temporary preparation directory owned by this fixture family.
        /// </summary>
        /// <param name="root">
        /// Absolute preparation root below the system temporary directory with the expected fixture prefix.
        /// </param>
        private static void DeleteOwnedPreparationRoot(string root)
        {
            var absolute = Path.GetFullPath(root);
            var expectedTempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(expectedTempRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(absolute).StartsWith("boe-t062-", StringComparison.Ordinal))
                throw new InvalidOperationException($"Refusing to remove unexpected preparation root '{absolute}'.");
            if (Directory.Exists(absolute)) Directory.Delete(absolute, recursive: true);
        }

        internal void AssertCarrierIdentityHistoryAgreement()
        {
            var player = Read(WoundCarrierCatalog.PlayerPath);
            var catalog = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(player, null, null, null, null));
            Assert.Empty(catalog.Issues);
            catalog.TryResolveOne(WoundId, out var occurrence);
            var identity = WoundIdentityState.Parse(File.ReadAllText(FileSystem.ResolvePath(WoundIdentityState.StatePath)), WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(File.ReadAllText(FileSystem.ResolvePath(WoundHistoryState.HistoryPath)), WoundHistoryState.HistoryPath);
            Assert.True(identity.IsValid, Issues(identity.Issues));
            Assert.True(history.IsValid, Issues(history.Issues));
            Assert.True(identity.State!.TryGetEntry(WoundId, out var entry));
            AssertSelectedWoundState(catalog, occurrence, entry!, history.State!, "recoveryBaseline");
            Assert.Empty(history.State!.ValidateAgreement(identity.State, catalog));
        }

        internal byte[] CaptureRecoveryStateBytes()
        {
            var wound = ReadActiveOrTerminalRecoveryWound(FileSystem, WoundId);
            return System.Text.Encoding.UTF8.GetBytes(wound["recovery"]!.ToJsonString());
        }

        internal void AssertRecoveryStateBytesEqual(byte[] expected) =>
            Assert.True(expected.AsSpan().SequenceEqual(CaptureRecoveryStateBytes()));

        internal byte[]? CaptureDeteriorationAnchorBytes()
            => CaptureDeteriorationAnchorBytes(FileSystem);

        internal byte[]? CaptureRecoveryAnchorBytes()
        {
            var wound = ReadActiveOrTerminalRecoveryWound(FileSystem, WoundId);
            return wound["recovery"]?["recoveryAnchor"] is { } anchor
                ? System.Text.Encoding.UTF8.GetBytes(anchor.ToJsonString())
                : null;
        }

        private static byte[]? CaptureDeteriorationAnchorBytes(FileSystemManager fs)
        {
            var identity = ReadRoot(fs, WoundIdentityState.StatePath);
            var selected = Assert.IsType<JsonObject>(Assert.Single(identity["entries"]!.AsArray()));
            var wound = ReadActiveOrTerminalRecoveryWound(fs, selected["woundId"]!.GetValue<string>());
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

        /// <summary>
        /// Captures one exact negative treatment source mutation in a fresh authenticated
        /// pending turn after releasing the positive preparation's lease and claims.
        /// </summary>
        /// <param name="mutation">
        /// The supported negative authority axis; unknown values throw without signing.
        /// </param>
        internal void PrepareSignedTreatmentAuthorityFault(string mutation)
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
                    PrepareCanonicalConsentWithdrawal();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }
            Write(FileSystem, "game_state/npcs/npc_core.json", npc);
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            PrepareLiveTurn(FileSystem, 45, "negative treatment authority " + mutation);
            Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
            var signedRequest = ReadRoot(FileSystem, LiveTurnPreparationService.TurnRequestPath);
            Assert.Equal("request_t062_45", signedRequest["requestId"]!.GetValue<string>());
        }

        /// <summary>
        /// Prepares canonical consent withdrawal with the existing narrow location-update
        /// planner before fresh signing, preserving the actual location origin and history.
        /// This produces fixture prerequisites rather than claiming common-plan publication.
        /// </summary>
        private void PrepareCanonicalConsentWithdrawal()
        {
            var worldMap = ReadRoot(FileSystem, MortalLocationMaterializationContract.WorldMapPath);
            var current = ReadRoot(FileSystem, MortalLocationMaterializationContract.CurrentLocationPath);
            var index = ReadRoot(FileSystem, MortalLocationIdentityState.StatePath);
            var originalLocation = Assert.IsType<JsonObject>(Assert.Single(worldMap["locations"]!.AsArray()));
            var locationId = originalLocation["locationId"]!.GetValue<string>();
            var customStates = originalLocation["customStates"]!.DeepClone().AsArray();
            var consent = Assert.Single(customStates.OfType<JsonObject>(), static row =>
                row["kind"]?.GetValue<string>() == MortalWoundTreatmentSceneAuthorityContract.ConsentKind);
            Assert.Equal("granted", consent["status"]!.GetValue<string>());
            consent["status"] = "withdrawn";
            var updates = new JsonObject
            {
                ["locationUpdates"] = new JsonArray(new JsonObject
                {
                    ["locationId"] = locationId,
                    ["customStates"] = customStates
                })
            };
            var signedRequest = ReadRoot(FileSystem, LiveTurnPreparationService.TurnRequestPath);
            Assert.Equal(Binding.Turn, signedRequest["turnNumber"]!.GetValue<int>());
            var updateTurn = checked(Binding.Turn + 1);
            var planned = MortalLocationAcceptedTurnPlanner.Build(new MortalLocationAcceptedTurnInput(
                worldMap, current, index, RawCurrentLocationData: null,
                RawWorldMapUpdates: updates, Turn: updateTurn));
            Assert.True(planned.Success, Issues(planned.Issues));
            Assert.Empty(planned.Issues);
            var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(planned.Plan);
            var finalCurrent = Assert.IsType<JsonObject>(plan.FinalCurrentLocation);
            var expectedMap = worldMap.DeepClone().AsObject();
            expectedMap["locations"]![0]!["customStates"] = customStates.DeepClone();
            Assert.True(JsonNode.DeepEquals(expectedMap, plan.FinalWorldMap));
            var expectedCurrent = current.DeepClone().AsObject();
            expectedCurrent["customStates"] = customStates.DeepClone();
            Assert.True(JsonNode.DeepEquals(expectedCurrent, finalCurrent));
            var originalEntry = Assert.IsType<JsonObject>(Assert.Single(index["locationEntries"]!.AsArray()));
            var finalEntry = Assert.IsType<JsonObject>(Assert.Single(plan.FinalIdentityIndex["locationEntries"]!.AsArray()));
            foreach (var property in originalEntry.Where(static property => property.Key != "transitions"))
                Assert.True(JsonNode.DeepEquals(property.Value, finalEntry[property.Key]), property.Key);
            Assert.True(JsonNode.DeepEquals(index["linkEntries"], plan.FinalIdentityIndex["linkEntries"]));
            var previousTransitions = originalEntry["transitions"]!.AsArray();
            var finalTransitions = finalEntry["transitions"]!.AsArray();
            Assert.Equal(previousTransitions.Count + 1, finalTransitions.Count);
            Assert.True(JsonNode.DeepEquals(previousTransitions, new JsonArray(finalTransitions
                .Take(previousTransitions.Count).Select(static transition => transition!.DeepClone()).ToArray())));
            var update = Assert.IsType<JsonObject>(finalTransitions[^1]);
            Assert.Equal("location_update", update["kind"]!.GetValue<string>());
            Assert.Equal(updateTurn, update["turn"]!.GetValue<int>());
            Assert.Equal(locationId, update["entityId"]!.GetValue<string>());
            var identity = MortalLocationIdentityState.Parse(plan.FinalIdentityIndex);
            Assert.Empty(identity.Issues);
            Assert.Empty(identity.ValidateCanonicalState(plan.FinalWorldMap));
            Assert.True(identity.IsAcceptedCanonicalLocation(finalCurrent));
            Assert.Equal(locationId, MortalLocationPlayerProjection.Create(
                plan.FinalWorldMap, finalCurrent, plan.FinalIdentityIndex).CurrentLocationId);
            Assert.Equal(new[]
            {
                MortalLocationMaterializationContract.WorldMapPath,
                MortalLocationMaterializationContract.CurrentLocationPath,
                MortalLocationIdentityState.StatePath
            }.OrderBy(static path => path, StringComparer.Ordinal),
                plan.TouchedPaths.OrderBy(static path => path, StringComparer.Ordinal));
            Write(FileSystem, MortalLocationMaterializationContract.WorldMapPath, plan.FinalWorldMap);
            Write(FileSystem, MortalLocationMaterializationContract.CurrentLocationPath, finalCurrent);
            Write(FileSystem, MortalLocationIdentityState.StatePath, plan.FinalIdentityIndex);
        }

        /// <summary>
        /// Verifies that genuine creation preserved every scenario treatment prerequisite
        /// rather than dropping the provider, consent, facility, and location requirements.
        /// </summary>
        internal void AssertPersistedTreatmentRequirements()
        {
            var wound = ReadCanonicalWound(FileSystem, WoundId);
            var canonical = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))!;
            var persisted = canonical["treatment"]!["routes"]![0]!["requirements"]!;
            var expected = Scenario.Wound["treatment"]!["routes"]![0]!["requirements"]!;
            Assert.True(JsonNode.DeepEquals(expected, persisted));
            Assert.Equal(new[] { "item_quantity", "skill_tier", "provider", "consent", "facility", "location" },
                persisted.AsArray().Select(requirement => requirement!["kind"]!.GetValue<string>()));
        }

        /// <summary>
        /// Calls the genuine six-argument procedure request factory with the exported
        /// current wound, complete canonical history, route, and signed accepted event.
        /// </summary>
        /// <param name="acceptedState">
        /// The current exported authority owned by this fixture's active lease.
        /// </param>
        /// <param name="operationKey">
        /// An unused exact operation identifier for this preparation-only request.
        /// </param>
        /// <returns>
        /// The actual factory result, including its sealed request or precise rejection issues.
        /// </returns>
        internal MortalWoundTreatmentAttemptRequestResult PrepareCurrentProcedureRequest(
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            string operationKey)
        {
            var history = WoundHistoryState.Parse(
                File.ReadAllText(FileSystem.ResolvePath(WoundHistoryState.HistoryPath)),
                WoundHistoryState.HistoryPath);
            Assert.True(history.IsValid, Issues(history.Issues));
            var wound = acceptedState.CurrentWound;
            var routeId = Assert.Single(wound.Treatment.Routes).RouteId;
            var eventRef = Assert.Single(acceptedState.Binding.AcceptedEvents).EventRef;
            return MortalWoundTreatmentPlanner.PrepareProcedureRequest(
                acceptedState, history, wound, operationKey, routeId, eventRef);
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

        /// <summary>
        /// Signs a fresh continuation at the selected canonical minute after the prior publication.
        /// </summary>
        /// <param name="turn">
        /// The distinct accepted turn number.
        /// </param>
        /// <param name="operation">
        /// The player operation recorded by the real turn preparation.
        /// </param>
        /// <param name="minute">
        /// The new canonical minute, or null to retain the scenario's original minute.
        /// </param>
        internal void PrepareFreshContinuationTurn(int turn, string operation, long? minute = null)
        {
            Write(FileSystem, EffectAcceptedTurnInputComposer.WorldTimePath,
                new JsonObject { ["currentTimeInMinutes"] = minute ?? Scenario.Minute });
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            PrepareLiveTurn(FileSystem, turn, operation);
            Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
            Binding = ExportRecoveryBinding(FileSystem, Lease, WoundId);
            Assert.Equal(turn, Binding.Turn);
            Assert.Equal($"request_t062_{turn}", Binding.RequestId);
        }

        /// <summary>
        /// Persists one targeted harmful occurrence before signing and publishes its
        /// worsening decision through the ordinary adapter and canonical normalizer.
        /// </summary>
        /// <param name="turn">
        /// The fresh decision turn following the current accepted source turn.
        /// </param>
        /// <param name="minute">
        /// The canonical minute at which retrauma reenters the deterioration condition.
        /// </param>
        /// <returns>
        /// The transition identity allocated by the genuine worsening publication.
        /// </returns>
        internal WorseningReentryPublication PublishSealedWorseningReentry(int turn, long minute)
        {
            var before = ReadCanonicalWound(FileSystem, WoundId);
            Assert.Equal("stabilized", before.Care.State);
            Assert.DoesNotContain("not_stabilized", before.Recovery.Blockers);
            const string narration =
                "Повторный удар разрывает уже сведённые края раны; прежняя стабилизация утрачена.";
            var occurrence = PrepareWorseningReentryOccurrence(
                FileSystem, Lease, Binding, before, turn);
            var decision = new JsonObject
            {
                ["opportunityRef"] = occurrence.OpportunityRef,
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
            var snapshotRead = PendingTurnSnapshotReader.ReadCurrent(
                FileSystem,
                Lease,
                new[]
                {
                    MortalWoundOccurrenceState.StatePath,
                    MortalWoundOpportunityReceiptState.StatePath,
                    WoundHistoryState.HistoryPath,
                    WoundCarrierCatalog.PlayerPath,
                    WoundCarrierCatalog.NpcPath,
                    WoundCarrierCatalog.EnemiesPath,
                    WoundCarrierCatalog.AlliesPath,
                    WoundCarrierCatalog.AfterlifeProfilesPath
                });
            Assert.True(snapshotRead.Success, Issues(snapshotRead.Issues));
            var snapshot = Assert.IsType<PendingTurnSnapshotReadAuthority>(snapshotRead.Snapshot);
            Assert.Equal(turn, snapshot.TurnNumber);
            Assert.Equal($"request_t062_{turn}", snapshot.RequestId);
            using var occurrenceStream = new MemoryStream(
                snapshot.ReadRequiredBytes(MortalWoundOccurrenceState.StatePath), writable: false);
            using var occurrenceReader = new StreamReader(
                occurrenceStream,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true);
            var signedOccurrences = MortalWoundOccurrenceState.Parse(
                occurrenceReader.ReadToEnd(), MortalWoundOccurrenceState.StatePath);
            Assert.True(signedOccurrences.IsValid, Issues(signedOccurrences.Issues));
            var signedOccurrence = Assert.Single(
                signedOccurrences.State!.Occurrences,
                value => value.OccurrenceId == occurrence.OccurrenceId);
            Assert.Equal(occurrence.OccurrenceFingerprint, signedOccurrence.OccurrenceFingerprint);
            foreach (var path in snapshot.CoveredLogicalPaths)
                Assert.Equal(snapshot.ReadRequiredBytes(path),
                    File.ReadAllBytes(FileSystem.ResolvePath(path)));
            var rebound = WoundAcceptedEventAuthorityComposer.RebindMortalOccurrence(
                signedOccurrence,
                signedOccurrences.State.Occurrences.Where(value =>
                    value.ProducerOperationKey == signedOccurrence.ProducerOperationKey).ToArray(),
                snapshot.SessionId,
                snapshot.RequestId,
                snapshot.SnapshotToken,
                snapshot.TurnNumber);
            Assert.True(rebound.Success, Issues(rebound.Issues));
            var sourceEvent = CreateWorseningReentrySource(signedOccurrence);
            var treeBeforeComposition = CaptureCanonicalTreeBytes();
            var composed = ComposeAcceptedWorseningResponse(
                FileSystem,
                Lease,
                sourceEvent,
                response);
            AssertCanonicalTreeBytesUnchanged(treeBeforeComposition);
            Assert.True(composed.Success, string.Join(" | ", composed.Issues.Select(
                static issue => $"{issue.Code}@{issue.FilePath}: expected={issue.Expected}; actual={issue.Actual}")));
            var opportunity = Assert.Single(composed.MaterializedOpportunities);
            Assert.Equal(signedOccurrence.OccurrenceId, opportunity.OpportunityId);
            Assert.Equal(snapshot.SnapshotToken, opportunity.SnapshotToken);
            Assert.Equal(rebound.EventsFingerprint, opportunity.AcceptedEventsFingerprint);
            var worseningDraft = Assert.Single(composed.Transitions);
            Assert.Equal("worsen", worseningDraft.Kind);
            Assert.Equal("untreated", worseningDraft.ProposedAfter.Care.State);
            Assert.Null(worseningDraft.ProposedAfter.Care.StabilizedAtTurn);
            Assert.Contains("not_stabilized", worseningDraft.ProposedAfter.Recovery.Blockers);
            Assert.Contains("contaminated", worseningDraft.ProposedAfter.Recovery.Blockers);
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            var distributor = new StateDistributor(
                FileSystem,
                NullLogger<StateDistributor>.Instance);
            distributor.DistributeAsync(response, composed).GetAwaiter().GetResult();
            var validationIssues = new ValidationService(
                    FileSystem,
                    NullLogger<ValidationService>.Instance)
                .ValidateAcceptedTurnRawResourceMaterializationAsync()
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
                    var parsed = WoundHistoryState.Parse(history.ToJsonString(), WoundHistoryState.HistoryPath);
                    Assert.True(parsed.IsValid, Issues(parsed.Issues));
                    var recoveryRow = Assert.Single(parsed.State!.Transitions,
                        row => row.WoundId == WoundId && row.TransitionResult is MortalWoundRecoveryPersistedResult);
                    var rawRecoveryRow = Assert.Single(transitions.OfType<JsonObject>(),
                        row => row["transitionId"]!.GetValue<string>() == recoveryRow.TransitionId);
                    var receipt = Assert.Single(FindProperties(
                        Assert.IsType<JsonObject>(rawRecoveryRow["transitionResult"]), "receiptFingerprint"));
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

        /// <summary>
        /// Persists and restores a genuine procedure command before publishing
        /// stabilization and settling its coordinated resource and dice claims.
        /// </summary>
        /// <param name="fs">
        /// The fixture filesystem containing the signed current turn and canonical wound.
        /// </param>
        /// <param name="lease">
        /// The active canonical write lease. It is released for external distribution
        /// and coordinated publication, then replaced with a fresh lease before return.
        /// </param>
        /// <param name="woundId">
        /// The exact canonical wound identifier selected for stabilization.
        /// </param>
        private static void ResolveAndPublishStabilization(
            FileSystemManager fs,
            ref FileSystemManager.CanonicalWriteLease? lease,
            string woundId)
        {
            var activeLease = Assert.IsType<FileSystemManager.CanonicalWriteLease>(lease);
            var planner = typeof(WoundMaterializationContract).Assembly.GetType(
                "BookOfEternityClient.Services.WoundAcceptedTurnPlanner", false, false);
            Assert.True(planner is not null, "T070 treatment publication is absent.");
            var compose = ExactStatic(planner!, "ComposeMortalWoundTreatmentPublication", 6);
            Assert.Equal(typeof(FileSystemManager), compose.GetParameters()[0].ParameterType);
            Assert.Equal(activeLease.GetType(), compose.GetParameters()[1].ParameterType);
            Assert.Equal(typeof(GameResponse), compose.GetParameters()[2].ParameterType);
            Assert.Equal("MortalWoundTreatmentAcceptedStateAuthority", compose.GetParameters()[3].ParameterType.Name);
            Assert.Contains("Request", compose.GetParameters()[4].ParameterType.Name, StringComparison.Ordinal);
            Assert.Contains("Resolution", compose.GetParameters()[5].ParameterType.Name, StringComparison.Ordinal);
            var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                ExportAcceptedState(fs, activeLease, woundId));
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
            AssertClosed(prepared, "IsValid", "Issues", "Request");
            var preparationIssues = Values(prepared, "Issues")
                .Select(Assert.IsType<ValidationIssue>)
                .ToArray();
            Assert.True(
                Assert.IsType<bool>(Required(prepared, "IsValid")),
                Issues(preparationIssues));
            Assert.Empty(preparationIssues);
            var request = Required(prepared, "Request");
            var resolutionResult = Invoke(ExactStatic(treatmentPlanner, "CreateProcedureAttempt", 4),
                request, history, before, acceptedState);
            AssertClosed(resolutionResult, "Disposition", "Issues", "ReplayReceipt", "Resolution");
            Assert.Equal("Resolved", Convert.ToString(Required(resolutionResult, "Disposition")));
            Assert.Empty(Values(resolutionResult, "Issues"));
            var resolution = Required(resolutionResult, "Resolution");
            const string finalSceneText =
                "The accepted stabilization is persisted before canonical publication.";
            var commandRoot = WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(
                binding,
                Assert.IsType<MortalWoundTreatmentResolution>(resolution),
                finalSceneText);
            var parsedCommand = WoundResponseInputComposer.ParseCommandRoot(
                JsonSerializer.SerializeToElement(commandRoot));
            Assert.True(parsedCommand.Success, Issues(parsedCommand.Issues));
            var recomposedCommand = WoundResponseInputComposer.RecomposeCommandRoot(
                binding,
                parsedCommand,
                Array.Empty<WoundOpportunityDecisionReceipt>());
            Assert.True(recomposedCommand.Success, Issues(recomposedCommand.Issues));
            Assert.True(JsonNode.DeepEquals(commandRoot, recomposedCommand.CommandRoot));
            activeLease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            lease = null;
            try
            {
                var modified = new StateDistributor(fs, NullLogger<StateDistributor>.Instance)
                    .DistributeAsync(
                        new GameResponse { Response = finalSceneText },
                        recomposedCommand)
                    .GetAwaiter().GetResult();
                Assert.Contains(AcceptedMechanicsPlan.WoundCommandPath, modified);
            }
            finally
            {
                lease = fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
            }
            activeLease = Assert.IsType<FileSystemManager.CanonicalWriteLease>(lease);
            Assert.True(JsonNode.DeepEquals(
                commandRoot,
                ReadRoot(fs, AcceptedMechanicsPlan.WoundCommandPath)));
            acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                ExportAcceptedState(fs, activeLease, woundId));
            AssertLiveTurnCorrelation(fs, acceptedState.Binding, input: null);
            history = WoundHistoryState.Parse(
                File.ReadAllText(fs.ResolvePath(WoundHistoryState.HistoryPath)),
                WoundHistoryState.HistoryPath);
            Assert.True(history.IsValid, Issues(history.Issues));
            var persistedRequests = acceptedState.RestorePersistedTreatmentRequests(history);
            Assert.True(persistedRequests.IsValid, Issues(persistedRequests.Issues));
            Assert.Empty(persistedRequests.Issues);
            var originalRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(request);
            request = Assert.Single(persistedRequests.Requests, candidate => string.Equals(
                candidate.RequestFingerprint,
                originalRequest.RequestFingerprint,
                StringComparison.Ordinal));
            before = ReadCanonicalWound(fs, woundId);
            resolutionResult = Invoke(ExactStatic(treatmentPlanner, "CreateProcedureAttempt", 4),
                request, history, before, acceptedState);
            AssertClosed(resolutionResult, "Disposition", "Issues", "ReplayReceipt", "Resolution");
            Assert.Equal("Resolved", Convert.ToString(Required(resolutionResult, "Disposition")));
            Assert.Empty(Values(resolutionResult, "Issues"));
            resolution = Required(resolutionResult, "Resolution");
            Assert.True(JsonNode.DeepEquals(commandRoot,
                WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(
                    acceptedState.Binding,
                    Assert.IsType<MortalWoundTreatmentResolution>(resolution),
                    finalSceneText)));
            var governedBefore = CaptureAllGovernedBytes(fs);
            var wholeTreeBefore = CaptureTreeBytes(fs.GameSessionPath);
            var composed = Invoke(compose, fs, activeLease, new GameResponse(), acceptedState, request, resolution);
            AssertClosed(composed, "IsValid", "Issues", "Plan");
            var compositionIssues = Values(composed, "Issues")
                .Select(Assert.IsType<ValidationIssue>)
                .ToArray();
            Assert.True(
                Assert.IsType<bool>(Required(composed, "IsValid")),
                Issues(compositionIssues));
            Assert.Empty(compositionIssues);
            var plan = Assert.IsType<AcceptedMechanicsPlan>(Required(composed, "Plan"));
            AssertPublishedWoundPlan(
                plan,
                Assert.IsType<AcceptedMechanicsWoundStageBundle>(plan.WoundStageBundle),
                woundId);
            AssertAllGovernedBytesUnchanged(fs, governedBefore);
            AssertTreeBytesUnchanged(fs, wholeTreeBefore);
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
                fs, activeLease, out _, out var cached));
            Assert.Same(plan, cached.Plan);
            activeLease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            lease = null;
            try
            {
                var published = AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                        fs,
                        new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance),
                        new ValidationService(fs, NullLogger<ValidationService>.Instance),
                        new Dictionary<string, string>(StringComparer.Ordinal))
                    .GetAwaiter().GetResult();
                var transaction = published.TreatmentResourcePublicationTransaction;
                try
                {
                    Assert.Empty(published.Issues);
                    Assert.Same(plan, published.MechanicsPlan);
                    Assert.NotNull(transaction);
                    var publishedTree = CaptureTreeBytes(fs.GameSessionPath);
                    var probe = transaction!.ProbeAsync(fs).GetAwaiter().GetResult();
                    Assert.True(probe.IsValid, Issues(probe.Issues));
                    Assert.Empty(probe.Issues);
                    Assert.Equal(0, probe.ChangedCount);
                    Assert.Equal(originalRequest.RequestFingerprint, probe.RequestFingerprint);
                    Assert.Equal(
                        plan.TreatmentResourcePublicationAuthority!.Finalization.FinalizationFingerprint,
                        probe.FinalizationFingerprint);
                    AssertTreeBytesUnchanged(fs, publishedTree);
                    var settled = transaction.CompleteAsync(fs).GetAwaiter().GetResult();
                    Assert.True(settled.IsValid, Issues(settled.Issues));
                    Assert.Empty(settled.Issues);
                    Assert.Equal(1, settled.ChangedCount);
                    Assert.Equal(
                        MortalWoundTreatmentPublicationTransactionOutcome.Finalized,
                        settled.Outcome);
                }
                finally
                {
                    if (transaction is not null)
                        ((IAsyncDisposable)transaction).DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            finally
            {
                lease = fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
            }
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

        /// <summary>
        /// Compares every published JSON afterimage and preserves exact bytes for
        /// all governed paths outside the accepted writes and deletions.
        /// </summary>
        /// <param name="fs">
        /// The filesystem whose canonical publication is checked.
        /// </param>
        /// <param name="beforeCompose">
        /// The exact governed baseline bytes, including missing paths.
        /// </param>
        /// <param name="plan">
        /// The accepted common plan that determines the expected publication.
        /// </param>
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
                using var stream = new MemoryStream(bytes!, writable: false);
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true);
                var actual = JsonNode.Parse(reader.ReadToEnd());
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

        /// <summary>
        /// Creates the readable retrauma proposal while retaining unrelated recovery blockers.
        /// Canonical anchors remain owned by the accepted worsening planner.
        /// </summary>
        /// <param name="before">
        /// The exact stabilized canonical wound before the new harmful occurrence.
        /// </param>
        /// <param name="narration">
        /// The player-visible explanation of the lost stabilization.
        /// </param>
        /// <returns>
        /// A physical worsening proposal without caller-authored canonical anchors.
        /// </returns>
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
            recovery["blockers"] = new JsonArray(before.Recovery.Blockers
                .Append("not_stabilized")
                .Distinct(StringComparer.Ordinal)
                .Select(static value => (JsonNode?)JsonValue.Create(value))
                .ToArray());
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

        /// <summary>
        /// Appends a targeted physical producer occurrence using the current signed source
        /// binding and persists the canonical roots required by the worsening adapter.
        /// </summary>
        /// <param name="fs">
        /// The isolated canonical fixture filesystem.
        /// </param>
        /// <param name="lease">
        /// The active lease protecting producer state before the next turn is signed.
        /// </param>
        /// <param name="binding">
        /// The exported binding of the actual current accepted source turn.
        /// </param>
        /// <param name="before">
        /// The stabilized canonical wound targeted by this retrauma occurrence.
        /// </param>
        /// <param name="turn">
        /// The later decision turn used to distinguish this producer operation and source.
        /// </param>
        /// <returns>
        /// The persisted occurrence with its generated identity and opportunity reference.
        /// </returns>
        private static MortalWoundOccurrence PrepareWorseningReentryOccurrence(
            FileSystemManager fs,
            FileSystemManager.CanonicalWriteLease lease,
            WoundAcceptedTurnBinding binding,
            WoundMaterializationEnvelope before,
            int turn)
        {
            AssertLiveTurnCorrelation(fs, binding, input: null);
            Assert.True(turn > binding.Turn);
            var sourceSnapshotRead = PendingTurnSnapshotReader.ReadCurrent(
                fs, lease, new[] { WoundCarrierCatalog.PlayerPath, WoundHistoryState.HistoryPath });
            Assert.True(sourceSnapshotRead.Success, Issues(sourceSnapshotRead.Issues));
            var sourceSnapshot = Assert.IsType<PendingTurnSnapshotReadAuthority>(sourceSnapshotRead.Snapshot);
            Assert.Equal(binding.SessionId, sourceSnapshot.SessionId);
            Assert.Equal(binding.RequestId, sourceSnapshot.RequestId);
            Assert.Equal(binding.SnapshotToken, sourceSnapshot.SnapshotToken);
            Assert.Equal(binding.Turn, sourceSnapshot.TurnNumber);

            var coordinates = binding.AcceptedEvents.Select(static value =>
                new WoundAcceptedResponseEventCoordinate(value.EventRef, value.Kind, value.AuthorityId))
                .ToArray();
            var selected = coordinates[0];
            const string readableCause = "Повторный удар снова раскрыл стабилизированную рану.";
            var projection = new WoundAcceptedResponseEventProjection(
                binding.SessionId, binding.RequestId, binding.SnapshotToken, binding.Turn, coordinates);
            var events = WoundAcceptedEventAuthorityComposer.Compose(projection,
                new[] { new WoundSelectedEventEvidence(0, new WoundOpportunityEventEvidence(
                    "formal", selected.Kind, selected.AuthorityId, "harmful", 3, readableCause)) });
            Assert.True(events.Success, Issues(events.Issues));
            var harm = new MortalWoundAcceptedHarmResult(
                0, 0, before.Owner, before.Classification.Domain, "mortal_formal_injury_v1",
                new MortalWoundOccurrenceSource("combat_action", $"combat_retrauma_t062_{turn}", "active"),
                new MortalWoundOccurrenceOutcome("harmful", 3, readableCause),
                4, null, null,
                new WoundOpportunitySafeContext("вы", "повторный удар",
                    new[] { "anatomical", "systemic", "other" }),
                new MortalWoundOccurrenceWorseningTarget(before.WoundId, "retrauma"));
            var producer = MortalWoundAcceptedProducerResult.CreateRegistered(
                "formal", $"producer_operation_retrauma_t062_{turn}", projection, new[] { harm });
            var candidate = new MortalWoundOccurrenceCandidate(
                producer.AcceptedResponse.SessionId, producer.AcceptedResponse.RequestId,
                producer.AcceptedResponse.SnapshotToken, producer.AcceptedResponse.Turn,
                producer.ProducerOperationKey, harm.ProducerCandidateOrdinal, producer.Harms.Count,
                producer.AdapterKind, harm.AcceptedEventOrdinal, events.Events, harm.Owner,
                harm.Domain, harm.ProfileKey, harm.Source, harm.Outcome, harm.HardMaximumSeverityRank,
                harm.MinimumSeverityRank, harm.GuaranteedTrigger, harm.SafeContext,
                harm.WorseningTarget, producer.SourceResultFingerprint);
            var pending = MortalWoundOccurrenceState.Parse(
                fs.ReadFileSync(MortalWoundOccurrenceState.StatePath) ??
                    "{\"schemaVersion\":1,\"occurrences\":[]}",
                MortalWoundOccurrenceState.StatePath);
            var receipts = MortalWoundOpportunityReceiptState.Parse(
                fs.ReadFileSync(MortalWoundOpportunityReceiptState.StatePath) ??
                    "{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}",
                MortalWoundOpportunityReceiptState.StatePath);
            Assert.True(pending.IsValid, Issues(pending.Issues));
            Assert.True(receipts.IsValid, Issues(receipts.Issues));
            var append = MortalWoundOccurrenceState.PlanAppend(
                pending.State!, new MortalWoundOccurrenceCandidateBatch(new[] { candidate }), receipts.State!);
            Assert.Equal("appended", append.Disposition);
            Assert.Empty(append.Issues);
            var appended = Assert.IsType<MortalWoundOccurrenceState>(append.State);
            fs.WriteFileAtomicAsync(lease, MortalWoundOccurrenceState.StatePath,
                MortalWoundOccurrenceState.SerializeCanonical(appended)).GetAwaiter().GetResult();
            if (fs.ReadFileSync(MortalWoundOpportunityReceiptState.StatePath) is null)
                fs.WriteFileAtomicAsync(lease, MortalWoundOpportunityReceiptState.StatePath,
                    MortalWoundOpportunityReceiptState.SerializeCanonical(receipts.State!)).GetAwaiter().GetResult();

            // Target uniqueness is resolved over required signed carrier bytes; this
            // fixture has no non-player wound entries or combat/afterlife actors.
            var emptyCarriers = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
            {
                [WoundCarrierCatalog.NpcPath] = new()
                {
                    ["schemaVersion"] = 1, ["entries"] = new JsonArray()
                },
                [WoundCarrierCatalog.EnemiesPath] = new() { ["enemiesData"] = new JsonArray() },
                [WoundCarrierCatalog.AlliesPath] = new() { ["alliesData"] = new JsonArray() },
                [WoundCarrierCatalog.AfterlifeProfilesPath] = new()
                {
                    ["schemaVersion"] = 1, ["profiles"] = new JsonArray()
                }
            };
            foreach (var pair in emptyCarriers)
                if (fs.ReadFileSync(pair.Key) is null)
                    fs.WriteFileAtomicAsync(lease, pair.Key, pair.Value.ToJsonString()).GetAwaiter().GetResult();
            var catalog = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
                ReadRoot(fs, WoundCarrierCatalog.PlayerPath),
                ReadRoot(fs, WoundCarrierCatalog.NpcPath),
                ReadRoot(fs, WoundCarrierCatalog.EnemiesPath),
                ReadRoot(fs, WoundCarrierCatalog.AlliesPath),
                ReadRoot(fs, WoundCarrierCatalog.AfterlifeProfilesPath)));
            Assert.Empty(catalog.Issues);
            Assert.True(catalog.TryResolveOne(before.WoundId, out var target));
            Assert.Equal(1, catalog.CountExactOccurrences(before.WoundId));
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(before),
                WoundMaterializationContract.SerializeCanonical(target.Wound));
            var persisted = MortalWoundOccurrenceState.Parse(
                fs.ReadFileSync(MortalWoundOccurrenceState.StatePath), MortalWoundOccurrenceState.StatePath);
            Assert.True(persisted.IsValid, Issues(persisted.Issues));
            return Assert.Single(persisted.State!.Occurrences,
                value => value.ProducerOperationKey == producer.ProducerOperationKey);
        }

        /// <summary>
        /// Projects GM-visible correlation only from the exact signed pending occurrence.
        /// </summary>
        /// <param name="occurrence">
        /// The persisted targeted occurrence recovered from the new signed snapshot.
        /// </param>
        /// <returns>
        /// Correlation JSON whose identity and semantics match that occurrence.
        /// </returns>
        private static JsonObject CreateWorseningReentrySource(MortalWoundOccurrence occurrence) => new()
        {
            ["schemaVersion"] = 1,
            ["adapterKind"] = occurrence.AdapterKind,
            ["acceptedEventOrdinal"] = occurrence.AcceptedEventOrdinal,
            ["opportunityRef"] = occurrence.OpportunityRef,
            ["owner"] = new JsonObject
            {
                ["realm"] = occurrence.Owner.Realm,
                ["ownerKind"] = occurrence.Owner.OwnerKind,
                ["ownerId"] = occurrence.Owner.OwnerId,
                ["carrierPath"] = occurrence.Owner.CarrierPath
            },
            ["domain"] = occurrence.Domain,
            ["profileKey"] = occurrence.ProfileKey,
            ["source"] = new JsonObject
            {
                ["kind"] = occurrence.Source.Kind,
                ["sourceId"] = occurrence.Source.SourceId,
                ["state"] = occurrence.Source.State
            },
            ["outcome"] = new JsonObject
            {
                ["kind"] = occurrence.Outcome.Kind,
                ["maximumSeverityRank"] = occurrence.Outcome.MaximumSeverityRank,
                ["readableCause"] = occurrence.Outcome.ReadableCause
            },
            ["safeContext"] = new JsonObject
            {
                ["target"] = occurrence.SafeContext.Target,
                ["cause"] = occurrence.SafeContext.Cause,
                ["allowedLocationKinds"] = new JsonArray(occurrence.SafeContext.AllowedLocationKinds
                    .Select(static value => (JsonNode?)JsonValue.Create(value)).ToArray())
            },
            ["worseningTarget"] = new JsonObject
            {
                ["woundId"] = occurrence.WorseningTarget!.WoundId,
                ["causeKind"] = occurrence.WorseningTarget.CauseKind
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

        /// <summary>
        /// Seeds genuine Mortal player resources before turn signing so wound effect
        /// continuation resolves against canonical definitions and owner authority.
        /// </summary>
        /// <param name="fs">
        /// The fresh fixture filesystem receiving the bootstrap resource quartet.
        /// </param>
        private static void WriteCanonicalResourceAuthority(FileSystemManager fs)
        {
            var bootstrap = ResourceBootstrapStateBuilder.BuildMortalPlayer(
                incarnationNumber: 1,
                turn: 1,
                permanentStrength: 10,
                permanentConstitution: 10,
                permanentIntelligence: 10,
                permanentWisdom: 10,
                permanentFaith: 10);
            Assert.True(bootstrap.IsValid, Issues(bootstrap.Issues));
            var definitions = Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions);
            var state = Assert.IsType<ResourceStateLedger>(bootstrap.State);
            var history = Assert.IsType<ResourceHistoryState>(bootstrap.History);
            Write(fs, ResourceMaterializationContract.DefinitionsPath,
                JsonNode.Parse(definitions.ToCanonicalJson())!.AsObject());
            Write(fs, ResourceMaterializationContract.StatePath,
                JsonNode.Parse(state.ToCanonicalJson())!.AsObject());
            Write(fs, ResourceMaterializationContract.HistoryPath,
                JsonNode.Parse(history.ToCanonicalJson())!.AsObject());
            var authority = CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                    definitions,
                    fs.ReadFileAsync,
                    state,
                    history,
                    CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap)
                .GetAwaiter().GetResult();
            Assert.True(authority.IsValid, Issues(authority.Issues));
            Assert.False(string.IsNullOrWhiteSpace(authority.CanonicalAuthorityJson));
            Write(fs, CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                JsonNode.Parse(authority.CanonicalAuthorityJson)!.AsObject());
        }

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
