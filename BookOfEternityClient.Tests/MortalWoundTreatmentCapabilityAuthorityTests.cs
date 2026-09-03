using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// RED contract for the T066 canonical-skill capability exporter.  The fixture is
/// deliberately source-shaped only: canonical accepted state, coordinates, proof
/// sealing, and final-plan after-images must remain production-owned.
/// </summary>
public sealed class MortalWoundTreatmentCapabilityAuthorityTests
{
    private const string CapabilityRef = "field_medicine_guaranteed_care";
    private const string SkillId = "skill_field_medicine_01";

    public static IEnumerable<object[]> CurrentExportRows => new[]
    {
        "player_active_skill_exports_physical_capability",
        "player_passive_skill_exports_physical_capability",
        "npc_active_skill_exports_physical_capability",
        "npc_passive_skill_exports_physical_capability",
        "provider_owned_proof_may_target_combatant",
        "provider_owned_proof_may_target_combatant_member",
        "target_owned_combatant_requires_promotion",
        "cross_root_player_npc_exact_skill_id_is_owner_scoped",
        "cross_root_player_npc_exact_capability_ref_is_owner_scoped",
        "cross_root_player_npc_confusable_skill_id_is_owner_scoped",
        "ordinary_capability_projection_comes_from_same_skill",
        "mastery_gate_remains_separate_skill_tier_requirement",
        "idless_extension_bearing_skill_rejects",
        "duplicate_skill_id_across_active_and_passive_rejects",
        "case_changed_skill_id_rejects",
        "unicode_confusable_skill_id_rejects",
        "duplicate_capability_ref_across_skill_kinds_rejects",
        "unicode_confusable_capability_ref_rejects",
        "inactive_skill_rejects",
        "retired_skill_rejects",
        "wrong_owner_rejects",
        "wrong_role_rejects",
        "wrong_realm_rejects",
        "spiritual_domain_rejects",
        "unknown_domain_rejects",
        "invalid_severity_envelope_rejects",
        "open_extension_field_rejects",
        "open_operation_limits_field_rejects",
        "aggregate_operation_limit_overflow_rejects",
        "all_zero_operation_limit_rejects",
        "source_semantic_change_changes_proof",
        "display_name_change_never_grants_authority",
        "proof_is_detached_and_immutable",
        "proof_contains_only_context_coordinates_owner_skill_capability_and_fingerprints"
    }.Select(static row => new object[] { DescribeScenario(row, publication: false) });

    public static IEnumerable<object[]> PublicationExportRows => new[]
    {
        "untouched_player_skill_reads_current_lease_bound_root",
        "untouched_npc_skill_reads_current_lease_bound_root",
        "untouched_player_passive_skill_reads_current_lease_bound_root",
        "untouched_npc_passive_skill_reads_current_lease_bound_root",
        "touched_player_active_skill_reads_exact_final_after_image",
        "touched_player_passive_skill_reads_exact_final_after_image",
        "touched_npc_active_skill_reads_exact_final_after_image",
        "touched_npc_passive_skill_reads_exact_final_after_image",
        "unchanged_final_source_reexports_equal_proof",
        "publication_export_accepts_no_detached_json_or_publication_intent"
    }.Select(static row => new object[] { DescribeScenario(row, publication: true) });

    public static IEnumerable<object[]> PublicationRejectionRows => new[]
    {
        "removed_final_skill_rejects",
        "retired_final_skill_rejects",
        "changed_final_skill_id_rejects",
        "changed_final_capability_ref_rejects",
        "changed_final_domain_rejects",
        "changed_final_operation_limits_reject",
        "changed_final_player_passive_operation_limits_reject",
        "changed_final_npc_passive_operation_limits_reject",
        "duplicate_or_confusable_final_skill_row_rejects",
        "confusable_final_skill_sibling_rejects",
        "stale_final_source_rejects"
    }.Select(static row => new object[] { DescribeScenario(row, publication: true) });

    [Fact]
    public void IndependentControl_ExistingActiveWoundFixtureStillParses()
    {
        var parsed = WoundMaterializationContract.Parse(
            WoundContractTestData.CreateActiveWound().ToJsonString(),
            "wound");

        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        Assert.NotNull(parsed.Wound);
    }

    [Theory]
    [InlineData("provider_owned_proof_may_target_combatant")]
    [InlineData("provider_owned_proof_may_target_combatant_member")]
    public void FixtureControl_CombatTargetHasOneAcceptedCarrierAndIdentity(string name)
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(name, publication: false));

        fixture.AssertAcceptedCombatWoundCarrier();
    }

    [Fact]
    public void FixtureControl_PristineEffectRootsProduceAcceptedEmptyMechanicsSnapshot()
    {
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario("player_active_skill_exports_physical_capability", publication: false));

        fixture.AssertPristineEffectMechanicsSnapshot();
    }

    [Fact]
    public void FixtureControl_SelectedSkillRowsUseExistingProductionMaterializationShapes()
    {
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario("player_active_skill_exports_physical_capability", publication: false));

        fixture.AssertProductionValidSkillShapes();
    }

    [Fact]
    public void FixtureControl_LiveTurnRequestMatchesAcceptedBinding()
    {
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario("player_active_skill_exports_physical_capability", publication: false));

        var exported = ExportAcceptedState(fixture);
        AssertValidResultShell(exported.IsValid, exported.Issues, exported.Authority, "accepted state");
        Assert.NotNull(exported.Binding);
        fixture.AssertPreparedLiveTurnMatchesExportedBinding(exported.Binding!);
    }

    [Fact]
    public void AcceptedStateExport_BindsThePreparedManifestToCanonicalSourceChanges()
    {
        using var baseline = CapabilityAuthorityFixture.Create(
            DescribeScenario("baseline_current_source", publication: false));
        using var changed = CapabilityAuthorityFixture.Create(
            DescribeScenario("source_semantic_change_changes_proof", publication: false));

        var baselineExport = ExportAcceptedState(baseline);
        var changedExport = ExportAcceptedState(changed);
        var baselineState = RequireAcceptedState(baselineExport);
        var changedState = RequireAcceptedState(changedExport);
        Assert.NotSame(baselineState, changedState);
        Assert.NotNull(baselineExport.Binding);
        Assert.NotNull(changedExport.Binding);
        var baselineBinding = Assert.IsType<WoundAcceptedTurnBinding>(baselineExport.Binding);
        var changedBinding = Assert.IsType<WoundAcceptedTurnBinding>(changedExport.Binding);
        Assert.NotEqual(
            baselineBinding.SnapshotToken,
            changedBinding.SnapshotToken);
        Assert.NotEqual(
            baselineBinding.AcceptedEventsFingerprint,
            changedBinding.AcceptedEventsFingerprint);
        var baselineEvent = Assert.Single(baselineBinding.AcceptedEvents,
            static value => value.EventRef == CapabilityAuthorityFixture.EventRef);
        var changedEvent = Assert.Single(changedBinding.AcceptedEvents,
            static value => value.EventRef == CapabilityAuthorityFixture.EventRef);
        Assert.Equal($"turn_{baselineBinding.Turn}:accepted_effect", baselineEvent.EventRef);
        Assert.Equal($"turn_{changedBinding.Turn}:accepted_effect", changedEvent.EventRef);
        Assert.Equal(CapabilityAuthorityFixture.EventKind, baselineEvent.Kind);
        Assert.Equal(CapabilityAuthorityFixture.EventKind, changedEvent.Kind);
        Assert.Equal(CapabilityAuthorityFixture.EventAuthorityId, baselineEvent.AuthorityId);
        Assert.Equal(CapabilityAuthorityFixture.EventAuthorityId, changedEvent.AuthorityId);
        Assert.NotEqual(baselineEvent.SemanticFingerprint, changedEvent.SemanticFingerprint);
    }

    [Theory]
    [MemberData(nameof(CurrentExportRows))]
    public void ExportCurrent_UsesOnlyCanonicalPlayerOrNpcSkillCapabilitySource(
        CapabilityScenario scenario)
    {
        using var fixture = CapabilityAuthorityFixture.Create(scenario);

        if (scenario.ExpectedBoundary == CapabilityFailureBoundary.Context)
        {
            AssertContextRejected(fixture, scenario);
            return;
        }

        if (scenario.ExpectedBoundary == CapabilityFailureBoundary.AcceptedState)
        {
            AssertAcceptedStateRejected(fixture, scenario);
            return;
        }

        var accepted = ExportAcceptedState(fixture);
        var acceptedState = RequireAcceptedState(accepted);
        var coordinates = CreateCoordinates(fixture, acceptedState);
        fixture.ApplyLiveExporterMutation();
        var result = InvokeCapabilityExporter(
            "ExportCurrent",
            acceptedState,
            coordinates,
            CapabilityRef,
            scenario.ActorRole);
        AssertCapabilityResult(result, scenario);
        if (result.IsValid)
        {
            AssertCapabilityProofBinding(result.Proof!, acceptedState, coordinates);
            var deterministicRetry = InvokeCapabilityExporter(
                "ExportCurrent",
                acceptedState,
                coordinates,
                CapabilityRef,
                scenario.ActorRole);
            AssertCapabilityResult(deterministicRetry, scenario);
            AssertCapabilityProofBinding(deterministicRetry.Proof!, acceptedState, coordinates);
            AssertEquivalentAuthority(result.Proof!, deterministicRetry.Proof!);
        }
        AssertScenarioSpecificCurrentSemantics(fixture, scenario, result);
    }

    [Fact]
    public void ExportCurrent_SameSessionDistinctAttemptCoordinatesShareOnlySourceFingerprint()
    {
        var scenario = DescribeScenario(
            "player_active_skill_exports_physical_capability",
            publication: false);
        using var fixture = CapabilityAuthorityFixture.Create(scenario);
        var acceptedState = RequireAcceptedState(ExportAcceptedState(fixture));
        var firstCoordinates = CreateCoordinates(
            fixture,
            acceptedState,
            "capability_authority_operation_001");
        var secondCoordinates = CreateCoordinates(
            fixture,
            acceptedState,
            "capability_authority_operation_002");

        var first = InvokeCapabilityExporter(
            "ExportCurrent",
            acceptedState,
            firstCoordinates,
            CapabilityRef,
            scenario.ActorRole);
        var second = InvokeCapabilityExporter(
            "ExportCurrent",
            acceptedState,
            secondCoordinates,
            CapabilityRef,
            scenario.ActorRole);

        AssertCapabilityResult(first, scenario);
        AssertCapabilityResult(second, scenario);
        Assert.NotNull(first.Proof);
        Assert.NotNull(second.Proof);
        Assert.Equal(
            ReadRequiredProperty(first.Proof!, "SourceSemanticFingerprint"),
            ReadRequiredProperty(second.Proof!, "SourceSemanticFingerprint"));
        Assert.NotEqual(
            ReadRequiredProperty(first.Proof!, "CoordinatesFingerprint"),
            ReadRequiredProperty(second.Proof!, "CoordinatesFingerprint"));
        Assert.NotEqual(
            ReadRequiredProperty(first.Proof!, "ProofFingerprint"),
            ReadRequiredProperty(second.Proof!, "ProofFingerprint"));
    }

    [Fact]
    public void CapabilityProofMinting_RequiresAnUnforgeableAuthorityOwnedCapability()
    {
        var assembly = typeof(WoundMaterializationContract).Assembly;
        var authorityType = Assert.IsAssignableFrom<Type>(assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentCapabilityAuthority",
            throwOnError: false,
            ignoreCase: false));
        var resultType = Assert.IsAssignableFrom<Type>(assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentCapabilityProofResult",
            throwOnError: false,
            ignoreCase: false));
        var proofType = Assert.IsAssignableFrom<Type>(resultType.GetProperty("Proof")!.PropertyType);
        Assert.Equal(
            new[] { "ExportCurrent", "ExportForPublication" },
            authorityType
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(static method => method.IsAssembly)
                .Select(static method => method.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));

        Assert.All(
            new[] { resultType, proofType },
            type => Assert.All(
                type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                static constructor => Assert.True(constructor.IsPrivate)));

        var mintingMethods = proofType
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == "Create")
            .Concat(resultType
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.Name == "Valid"))
            .ToArray();
        Assert.Equal(2, mintingMethods.Length);
        foreach (var method in mintingMethods)
        {
            Assert.True(method.IsAssembly);
            var capabilityParameter = Assert.Single(
                method.GetParameters(),
                parameter => parameter.ParameterType.DeclaringType == authorityType &&
                             parameter.ParameterType.Name == "ProofMintCapability");
            var capabilityType = capabilityParameter.ParameterType;
            Assert.True(capabilityType.IsSealed);
            Assert.DoesNotContain(
                authorityType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                field => field.FieldType == capabilityType && !field.IsPrivate);
            Assert.DoesNotContain(
                authorityType.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                property => property.PropertyType == capabilityType &&
                            property.GetMethod is { IsPrivate: false });
            Assert.DoesNotContain(
                authorityType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                method => method.ReturnType == capabilityType && !method.IsPrivate);

            var rejected = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, method.GetParameters().Select(static _ => (object?)null).ToArray()));
            Assert.IsType<InvalidOperationException>(rejected.InnerException);

            var forgedCapability = Assert.IsAssignableFrom<object>(
                Activator.CreateInstance(capabilityType, nonPublic: true));
            var forgedArguments = method.GetParameters()
                .Select(parameter => parameter == capabilityParameter ? forgedCapability : null)
                .ToArray();
            var forgedRejected = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, forgedArguments));
            Assert.IsType<InvalidOperationException>(forgedRejected.InnerException);
        }
    }

    [Fact]
    public void ExportCurrent_StabilizeAppliesToFreshUntreatedWoundWithoutRecoveryBlockers()
    {
        var scenario = DescribeScenario(
            "fresh_untreated_empty_blockers_stabilize_exports",
            publication: false);
        using var fixture = CapabilityAuthorityFixture.Create(scenario);
        Assert.Equal("untreated", fixture.Before.Care.State);
        Assert.Empty(fixture.Before.Recovery.Blockers);

        var result = InvokeExportCurrent(fixture, scenario);

        AssertCapabilityResult(result, scenario);
    }

    [Fact]
    public void PublicationPrerequisite_EffectlessUntreatedWoundAcceptsGuaranteedStabilization()
    {
        var scenario = DescribeScenario(
            "untouched_player_skill_reads_current_lease_bound_root",
            publication: true);
        using var fixture = CapabilityAuthorityFixture.Create(scenario);
        var candidate = fixture.Before with
        {
            Care = fixture.Before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = fixture.Before.LastTransition.Turn
            },
            Recovery = fixture.Before.Recovery with
            {
                Blockers = fixture.Before.Recovery.Blockers
                    .Where(static blocker => !string.Equals(
                        blocker,
                        "not_stabilized",
                        StringComparison.Ordinal))
                    .ToImmutableArray()
            }
        };
        var roundTrip = WoundMaterializationContract.Parse(
            WoundMaterializationContract.SerializeCanonical(candidate),
            "t070.publicationPrerequisite");
        Assert.True(roundTrip.IsValid, DescribeIssues(roundTrip.Issues));

        var simulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(
            fixture.Before,
            new[]
            {
                ImmutableArray.Create<MortalWoundTreatmentOperation>(
                    new MortalWoundStabilizeOperation())
            });

        Assert.True(simulation.IsApplicable);
        Assert.True(simulation.Improved);
        Assert.NotNull(simulation.WorkingWound);
    }

    [Fact]
    public void AcceptedStateRequirementContextProjection_IsValidOnlyWhileOriginAuthorityIsCurrent()
    {
        var scenario = DescribeScenario(
            "player_active_skill_exports_physical_capability",
            publication: false);
        using var fixture = CapabilityAuthorityFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            RequireAcceptedState(ExportAcceptedState(fixture)));
        var marked = acceptedState.RequirementContext;
        var copied = marked with { };
        var parsed = MortalWoundTreatmentAuthority.ParseContext(
            fixture.ContextRoot.ToJsonString(),
            "generic_context.json");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var generic = Assert.IsType<MortalWoundTreatmentAuthority.Context>(parsed.Context);

        Assert.True(MortalWoundTreatmentAcceptedStateAuthority
            .IsAcceptedStateContextProjection(marked));
        Assert.False(MortalWoundTreatmentAcceptedStateAuthority
            .IsAcceptedStateContextProjection(copied));
        Assert.False(MortalWoundTreatmentAcceptedStateAuthority
            .IsAcceptedStateContextProjection(generic));

        fixture.ReleaseLeaseForTopLevelPublication();

        Assert.False(MortalWoundTreatmentAcceptedStateAuthority
            .IsAcceptedStateContextProjection(marked));
    }

    [Fact]
    public void ExportForPublication_InvalidRolePrecedesNullPlanAuthentication()
    {
        var scenario = DescribeScenario(
            "player_active_skill_exports_physical_capability",
            publication: false);
        using var fixture = CapabilityAuthorityFixture.Create(scenario);
        var acceptedState = RequireAcceptedState(ExportAcceptedState(fixture));
        var coordinates = CreateCoordinates(fixture, acceptedState);

        var result = InvokeCapabilityExporter(
            "ExportForPublication",
            acceptedState,
            coordinates,
            CapabilityRef,
            "invalid_actor_role",
            publicationPlan: null,
            includeNullPublicationPlan: true);

        Assert.False(result.IsValid);
        Assert.Null(result.Proof);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("mortal_wound_treatment_capability_binding_mismatch", issue.Code);
        Assert.Equal("treatmentCapability.actorRole", issue.FilePath);
    }

    [Theory]
    [MemberData(nameof(PublicationExportRows))]
    public void ExportForPublication_RevalidatesTheExactCurrentOrFinalSkillSource(
        CapabilityScenario scenario)
    {
        using var fixture = CapabilityAuthorityFixture.Create(scenario);

        var result = InvokeExportForPublication(fixture, scenario);
        AssertCapabilityResult(result, scenario);
    }

    [Theory]
    [MemberData(nameof(PublicationRejectionRows))]
    public void T070Publication_RejectsInvalidFinalSkillSourceWithoutCachingAnInvalidPlan(
        CapabilityScenario scenario)
    {
        using var fixture = CapabilityAuthorityFixture.Create(scenario);

        var rejected = ComposeRejectedPublicationFlow(fixture);

        AssertInvalidResultShell(
            rejected.IsValid,
            rejected.Issues,
            rejected.Value,
            scenario,
            "T070 publication");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
    }

    [Fact]
    public void PublicationPlan_GuaranteedStabilizationPublishesFromUntouchedPlayerSkill()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "untouched_player_skill_reads_current_lease_bound_root",
            publication: true));

        fixture.AssertUnrelatedEffectMechanicsSnapshot();
        var flow = ComposePublicationFlow(fixture);
        var beforeRetry = fixture.CaptureGovernedPublicationBeforeImages();
        var retry = InvokeT070Publication(
            fixture,
            flow.AcceptedState,
            flow.Request,
            flow.Resolution);
        AssertValidResultShell(
            retry.IsValid,
            retry.Issues,
            retry.Value,
            "T070 exact guaranteed stabilization retry");
        Assert.Same(flow.Plan, retry.Value);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var cachedRetry));
        Assert.True(cachedRetry.Success, DescribeIssues(cachedRetry.Issues));
        Assert.Same(flow.Plan, cachedRetry.Plan);
        fixture.AssertGovernedRootsMatchBeforeImages(beforeRetry);

        var beforeImages = fixture.CaptureGovernedPublicationBeforeImages();
        var published = PublishCachedAcceptedPlan(fixture);

        Assert.Same(flow.Plan, published);
        var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(
            published.WoundStageBundle);
        Assert.Equal(bundle.InputFingerprint, bundle.PreparedPlan.InputFingerprint);
        Assert.Equal(
            bundle.WoundPreparationFingerprint,
            bundle.EffectBatchPlan.WoundPreparationFingerprint);
        Assert.Equal(
            bundle.EffectInputFingerprint,
            bundle.FinalPlan.EffectInputFingerprint);
        Assert.Equal(
            bundle.EffectAcceptedTurnPlanFingerprint,
            bundle.FinalPlan.EffectAcceptedTurnPlanFingerprint);
        Assert.Equal(
            bundle.WoundFinalPlanFingerprint,
            bundle.FinalPlan.WoundFinalPlanFingerprint);
        Assert.False(string.IsNullOrWhiteSpace(bundle.BundleFingerprint));
        Assert.Empty(bundle.ApplicationResults);
        Assert.Empty(bundle.TerminationResults);
        fixture.AssertPublicationPublishedExactSkillRoots(flow.Plan, beforeImages);
        fixture.AssertUnrelatedGlobalEffectPreserved(beforeImages);
        var wound = ReadPublishedPlayerWound(fixture);
        var requestCoordinates = ReadRequiredProperty(flow.Request, "Coordinates");
        Assert.Equal("stabilized", wound.Care.State);
        Assert.Equal(
            Convert.ToInt32(ReadRequiredProperty(flow.Coordinates, "Turn")),
            wound.Care.StabilizedAtTurn);
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(requestCoordinates, "AttemptId")),
            wound.Care.LastAttemptId);
        Assert.Contains(CapabilityAuthorityFixture.RouteId, wound.Treatment.CompletedRouteIds);
        Assert.DoesNotContain("not_stabilized", wound.Recovery.Blockers);
        Assert.Contains("unsafe_environment", wound.Recovery.Blockers);
        var recoveryAnchor = Assert.IsType<WoundRecoveryAnchor>(
            wound.Recovery.RecoveryAnchor);
        Assert.Equal("stabilization", recoveryAnchor.AnchorKind);
        Assert.Equal(1_260, recoveryAnchor.AnchorMinute);
        Assert.Null(wound.Recovery.DeteriorationAnchor);

        var history = fixture.ReadCurrentHistory();
        var transition = Assert.Single(
            history.State!.Transitions,
            static row => string.Equals(row.Kind, "treat", StringComparison.Ordinal));
        Assert.Equal(
            ReadRequiredProperty(requestCoordinates, "OperationKey"),
            transition.OperationKey);
        Assert.Equal(
            ReadRequiredProperty(requestCoordinates, "AttemptId"),
            transition.AttemptId);
        Assert.False(transition.Terminal);
        Assert.NotNull(transition.TreatmentResult);
        Assert.Null(transition.CourseId);
        Assert.Null(transition.CourseMilestoneOrdinal);
        Assert.Null(transition.PaymentFingerprint);
        Assert.Equal(
            ReadRequiredProperty(flow.Request, "RequestFingerprint"),
            transition.SourceFingerprint);
        const string treatmentSummary =
            "The accepted treatment result is retained for replay.";
        Assert.Equal(treatmentSummary, transition.ReadableSummary);
        Assert.Equal(
            WoundHistoryState.ComputeOutputFingerprint(
                transition.OperationKey,
                transition.EventRef,
                treatmentSummary),
            transition.OutputFingerprint);
        Assert.Equal(transition.TransitionId, recoveryAnchor.AnchorTransitionId);
    }

    [Fact]
    public void PublicationPlan_GuaranteedStabilizationPreservesUnrelatedDeteriorationAnchor()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "untouched_player_skill_preserves_unrelated_deterioration_anchor",
            publication: true));

        var flow = ComposePublicationFlow(fixture);
        var published = PublishCachedAcceptedPlan(fixture);

        Assert.Same(flow.Plan, published);
        var wound = ReadPublishedPlayerWound(fixture);
        var deteriorationAnchor = Assert.IsType<WoundDeteriorationAnchor>(
            wound.Recovery.DeteriorationAnchor);
        var recoveryAnchor = Assert.IsType<WoundRecoveryAnchor>(
            wound.Recovery.RecoveryAnchor);
        Assert.Equal("stabilization", recoveryAnchor.AnchorKind);
        Assert.Equal(1_260, recoveryAnchor.AnchorMinute);
        Assert.Equal("unsafe_environment", deteriorationAnchor.ConditionKey);
        Assert.Equal(100, deteriorationAnchor.AnchorMinute);
        Assert.Equal(
            "wound_transition_test_001",
            deteriorationAnchor.AnchorTransitionId);
        var transition = Assert.Single(
            fixture.ReadCurrentHistory().State!.Transitions,
            static row => string.Equals(row.Kind, "treat", StringComparison.Ordinal));
        Assert.Equal(transition.TransitionId, recoveryAnchor.AnchorTransitionId);
    }

    [Fact]
    public void PublicationPlan_FinalCapabilityMutationRejectsWithoutCachingOrWriting()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "untouched_player_skill_reads_current_lease_bound_root",
            publication: true));

        var attempt = PreparePublicationAttempt(fixture);
        fixture.MutatePersistedSelectedCapabilityAfterExport();
        var beforeAdmission = fixture.CaptureGovernedPublicationBeforeImages();

        var rejected = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution);

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Value);
        Assert.Contains(
            rejected.Issues,
            static issue =>
                string.Equals(
                    issue.Code,
                    "mortal_wound_treatment_capability_publication_mismatch",
                    StringComparison.Ordinal) &&
                string.Equals(
                    issue.FilePath,
                    "treatmentCapability.publicationPlan",
                    StringComparison.Ordinal));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        fixture.AssertGovernedRootsMatchBeforeImages(beforeAdmission);
    }

    [Fact]
    public void PublicationPlan_ChangedSealedSemanticsRejectWithoutReplacingAcceptedPlanOrWriting()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "untouched_player_skill_reads_current_lease_bound_root",
            publication: true));

        var first = ComposePublicationFlow(fixture);
        var changedAttempt = PreparePublicationAttempt(
            fixture,
            CapabilityAuthorityFixture.OperationKey + "_changed");
        var beforeConflict = fixture.CaptureGovernedPublicationBeforeImages();

        var rejected = InvokeT070Publication(
            fixture,
            changedAttempt.AcceptedState,
            changedAttempt.Request,
            changedAttempt.Resolution);

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Value);
        Assert.NotEmpty(rejected.Issues);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var cached));
        Assert.True(cached.Success, DescribeIssues(cached.Issues));
        Assert.Same(first.Plan, cached.Plan);
        fixture.AssertGovernedRootsMatchBeforeImages(beforeConflict);
    }

    [Fact]
    public void PublicationAdmission_RejectsForeignOrdinaryBundleAndContinuationRequestMix()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "untouched_player_skill_reads_current_lease_bound_root",
            publication: true));
        var first = ComposePublicationFlow(fixture);
        var changed = PreparePublicationAttempt(
            fixture,
            CapabilityAuthorityFixture.OperationKey + "_foreign");
        var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(
            Assert.IsType<AcceptedMechanicsPlan>(first.Plan).WoundStageBundle);
        var continuationAuthority = Assert.IsAssignableFrom<object>(
            bundle.PreparedPlan.TreatmentContinuationAuthority);

        var ordinaryInput = WoundEffectBatchPlannerTests
            .CreateNoMechanicsInputForAcceptedCache();
        var ordinaryPrepared = WoundAcceptedTurnPlanner.Prepare(ordinaryInput);
        Assert.True(ordinaryPrepared.Success, DescribeIssues(ordinaryPrepared.Issues));
        var ordinaryEffectInput = WoundEffectBatchPlannerTests
            .CreateEffectInputForAcceptedCache(ordinaryPrepared.Plan!);
        var ordinaryEffect = WoundEffectBatchPlanner.Build(
            ordinaryPrepared.Plan!,
            ordinaryEffectInput,
            new EffectIdentityFactory());
        Assert.True(ordinaryEffect.Success, DescribeIssues(ordinaryEffect.Issues));
        var ordinaryFinal = WoundAcceptedTurnPlanner.Finalize(
            ordinaryPrepared.Plan!,
            ordinaryEffect);
        Assert.True(ordinaryFinal.Success, DescribeIssues(ordinaryFinal.Issues));
        var ordinaryBundle = new AcceptedMechanicsWoundStageBundle(
            ordinaryInput,
            ordinaryPrepared.Plan!,
            ordinaryEffect.Plan!,
            ordinaryFinal.Plan!);

        var foreign = AcceptedMechanicsWoundCommonInputComposer
            .ComposeTreatmentContinuation(
                fixture.FileSystem,
                fixture.Lease,
                ordinaryBundle,
                continuationAuthority,
                new object(),
                1_260);
        Assert.False(foreign.Success);
        Assert.Contains(foreign.Issues, static issue =>
            issue.Code == "accepted_mechanics_wound_treatment_continuation_provenance_mismatch");

        var mixedIssues = MortalWoundTreatmentCapabilityAuthority
            .CandidateAdmissionGate.Validate(
                Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(changed.AcceptedState),
                Assert.IsType<MortalWoundTreatmentAttemptRequest>(changed.Request),
                Assert.IsType<MortalWoundTreatmentResolution>(changed.Resolution),
                Assert.IsType<AcceptedMechanicsPlan>(first.Plan),
                ComputePublicationSemanticFingerprint(changed),
                continuationAuthority);
        Assert.Contains(mixedIssues, static issue =>
            issue.Code == "mortal_wound_treatment_publication_provenance_mismatch");
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var cached));
        Assert.Same(first.Plan, cached.Plan);
    }

    [Fact]
    public void PublicationPlan_MalformedStabilizeIntentSealRejectsWithoutCachingOrWriting()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "untouched_player_skill_reads_current_lease_bound_root",
            publication: true));
        var attempt = PreparePublicationAttempt(fixture);
        var source = Assert.IsType<MortalWoundTreatmentResolution>(attempt.Resolution);
        var original = Assert.IsType<MortalWoundStabilizeOutcomeIntent>(
            Assert.Single(source.OutcomeIntents));
        var malformed = MortalWoundStabilizeOutcomeIntent.Create(
            ordinal: 0,
            original.DeclaredOperationFingerprint,
            "sha256:" + new string('f', 64));
        var forged = RecreateResolution(source, new[] { malformed });
        Assert.Equal(
            source.ResolutionAuthorityFingerprint,
            forged.ResolutionAuthorityFingerprint);
        Assert.Equal(source.ResultFingerprint, forged.ResultFingerprint);
        Assert.NotEqual(
            original.IntentFingerprint,
            Assert.Single(forged.OutcomeIntents).IntentFingerprint);
        var before = fixture.CaptureGovernedPublicationBeforeImages();

        var rejected = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            forged);

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Value);
        Assert.Contains(rejected.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_publication_slice_unsupported");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        fixture.AssertGovernedRootsMatchBeforeImages(before);
    }

    [Fact]
    public async Task PublicationPlan_ConcurrentDifferentSemanticsCannotPassOneInProgressReservation()
    {
        var armed = 0;
        var observed = 0;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario(
                "untouched_player_skill_reads_current_lease_bound_root",
                publication: true),
            new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path =>
                {
                    if (Volatile.Read(ref armed) == 1 &&
                        Interlocked.CompareExchange(ref observed, 1, 0) == 0)
                    {
                        entered.Set();
                        release.Wait();
                    }
                    return Task.CompletedTask;
                }
            });
        var first = PreparePublicationAttempt(fixture);
        var second = PreparePublicationAttempt(
            fixture,
            CapabilityAuthorityFixture.OperationKey + "_concurrent");
        Volatile.Write(ref armed, 1);

        var firstTask = Task.Run(() => InvokeT070Publication(
            fixture,
            first.AcceptedState,
            first.Request,
            first.Resolution));
        TypedResultView? conflicting = null;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(15)));
            conflicting = InvokeT070Publication(
                fixture,
                second.AcceptedState,
                second.Request,
                second.Resolution);
        }
        finally
        {
            release.Set();
        }
        var admitted = await firstTask;

        AssertValidResultShell(
            admitted.IsValid,
            admitted.Issues,
            admitted.Value,
            "reserved first treatment publication");
        Assert.NotNull(conflicting);
        Assert.False(conflicting.IsValid);
        Assert.Contains(conflicting.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_publication_conflict");
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var cached));
        Assert.Same(admitted.Value, cached.Plan);
    }

    [Fact]
    public async Task PublicationPlan_StaleCancelledReservationCannotEraseLaterCommittedPlan()
    {
        var armed = 0;
        var observed = 0;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario(
                "untouched_player_skill_reads_current_lease_bound_root",
                publication: true),
            new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path =>
                {
                    if (Volatile.Read(ref armed) == 1 &&
                        Interlocked.CompareExchange(ref observed, 1, 0) == 0)
                    {
                        entered.Set();
                        release.Wait();
                    }
                    return Task.CompletedTask;
                }
            });
        var staleAttempt = PreparePublicationAttempt(fixture);
        var laterAttempt = PreparePublicationAttempt(
            fixture,
            CapabilityAuthorityFixture.OperationKey + "_later_committed");
        var before = fixture.CaptureGovernedPublicationBeforeImages();
        Volatile.Write(ref armed, 1);

        var staleTask = Task.Run(() => InvokeT070Publication(
            fixture,
            staleAttempt.AcceptedState,
            staleAttempt.Request,
            staleAttempt.Resolution));
        TypedResultView? later = null;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(15)));
            AcceptedMechanicsPlanAuthority.InvalidateValidated(
                fixture.FileSystem,
                fixture.Lease);
            later = InvokeT070Publication(
                fixture,
                laterAttempt.AcceptedState,
                laterAttempt.Request,
                laterAttempt.Resolution);
            AssertValidResultShell(
                later.IsValid,
                later.Issues,
                later.Value,
                "later committed treatment publication");
        }
        finally
        {
            release.Set();
        }
        var stale = await staleTask;

        Assert.NotNull(later);
        Assert.False(stale.IsValid);
        Assert.Null(stale.Value);
        Assert.Contains(stale.Issues, static issue =>
            issue.Code is "mortal_wound_treatment_publication_conflict" or
                "mortal_wound_treatment_publication_reservation_invalid");
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var cached));
        Assert.True(cached.Success, DescribeIssues(cached.Issues));
        Assert.Same(later.Value, cached.Plan);
        Assert.Equal(
            ReadRequiredProperty(later.Value!, "PreparedPlanFingerprint"),
            ReadRequiredProperty(cached.Plan!, "PreparedPlanFingerprint"));
        fixture.AssertGovernedRootsMatchBeforeImages(before);
    }

    [Fact]
    public async Task PublicationPlan_StaleReservationCannotRegisterTreatmentItemsAfterLaterCommit()
    {
        var armed = 0;
        var observed = 0;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario(
                "untouched_player_skill_reads_current_lease_bound_root",
                publication: true),
            new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path =>
                {
                    if (Volatile.Read(ref armed) == 1 &&
                        string.Equals(
                            path,
                            MortalItemIdentityState.StatePath,
                            StringComparison.Ordinal) &&
                        Interlocked.CompareExchange(ref observed, 1, 0) == 0)
                    {
                        entered.Set();
                        release.Wait();
                    }
                    return Task.CompletedTask;
                }
            });
        var staleAttempt = PreparePublicationAttempt(fixture);
        var laterAttempt = PreparePublicationAttempt(
            fixture,
            CapabilityAuthorityFixture.OperationKey + "_item_later_committed");
        var before = fixture.CaptureGovernedPublicationBeforeImages();
        Volatile.Write(ref armed, 1);

        var staleTask = Task.Run(() => InvokeT070Publication(
            fixture,
            staleAttempt.AcceptedState,
            staleAttempt.Request,
            staleAttempt.Resolution));
        TypedResultView? later = null;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(15)));
            AcceptedMechanicsPlanAuthority.InvalidateValidated(
                fixture.FileSystem,
                fixture.Lease);
            later = InvokeT070Publication(
                fixture,
                laterAttempt.AcceptedState,
                laterAttempt.Request,
                laterAttempt.Resolution);
            AssertValidResultShell(
                later.IsValid,
                later.Issues,
                later.Value,
                "later publication committed before stale item registration");
        }
        finally
        {
            release.Set();
        }
        var stale = await staleTask;

        Assert.NotNull(later);
        Assert.False(stale.IsValid);
        Assert.Contains(stale.Issues, static issue =>
            issue.Code ==
                "mortal_wound_treatment_publication_reservation_invalid");
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var cached));
        Assert.Same(later.Value, cached.Plan);
        fixture.AssertGovernedRootsMatchBeforeImages(before);
    }

    [Theory]
    [InlineData("untouched_player_passive_skill_reads_current_lease_bound_root")]
    [InlineData("untouched_npc_passive_skill_reads_current_lease_bound_root")]
    [InlineData("touched_player_active_skill_reads_exact_final_after_image")]
    [InlineData("touched_player_passive_skill_reads_exact_final_after_image")]
    [InlineData("touched_npc_active_skill_reads_exact_final_after_image")]
    [InlineData("touched_npc_passive_skill_reads_exact_final_after_image")]
    [InlineData("unchanged_final_source_reexports_equal_proof")]
    public void PublicationPlan_NormalizerPublishesTheCachedPlanWithExactSkillRootAfterImages(
        string name)
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(name, publication: true));

        var flow = ComposePublicationFlow(fixture);
        var beforeImages = fixture.CaptureGovernedPublicationBeforeImages();
        var published = PublishCachedAcceptedPlan(fixture);

        Assert.Same(flow.Plan, published);
        fixture.AssertPublicationPublishedExactSkillRoots(flow.Plan, beforeImages);
    }

    [Fact]
    public void PublicationPlan_StaleBeforeImageRejectsBeforeAnyGovernedCanonicalWrite()
    {
        var mutationProbe = new PublicationFailureInjection();
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario(
                "touched_player_active_skill_reads_exact_final_after_image",
                publication: true),
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationAsync = mutationProbe.BeforeCanonicalMutationAsync
            });

        _ = ComposePublicationFlow(fixture);
        var beforeImages = fixture.CaptureGovernedPublicationBeforeImages();
        fixture.MutateGovernedRootAfterComposition();
        var staleImages = fixture.CaptureGovernedPublicationBeforeImages();
        var beforeBytes = beforeImages[fixture.SelectedPublicationRootPath].Bytes;
        var staleBytes = staleImages[fixture.SelectedPublicationRootPath].Bytes;
        Assert.NotNull(beforeBytes);
        Assert.NotNull(staleBytes);
        Assert.False(beforeBytes.AsSpan().SequenceEqual(staleBytes));
        mutationProbe.ResetObservation();

        var exception = Assert.Throws<InvalidDataException>(() => PublishCachedAcceptedPlan(fixture));
        Assert.Equal(
            $"Accepted mechanics authority at '{fixture.SelectedPublicationRootPath}' changed after validation.",
            exception.Message);
        Assert.Equal(0, mutationProbe.ObservedMutationCount);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        fixture.AssertGovernedRootsMatchBeforeImages(staleImages);
    }

    [Fact]
    public void PublicationPlan_InjectedLaterCanonicalWriteFailureRollsBackEveryGovernedRoot()
    {
        var fault = new PublicationFailureInjection();
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario("touched_player_active_skill_reads_exact_final_after_image", publication: true),
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationAsync = fault.BeforeCanonicalMutationAsync
            });

        var flow = ComposePublicationFlow(fixture);
        var beforeImages = fixture.CaptureGovernedPublicationBeforeImages();
        var selectedBefore = beforeImages[fixture.SelectedPublicationRootPath].Bytes;
        Assert.NotNull(selectedBefore);
        fault.ArmAfterCanonicalChange(
            WoundHistoryState.HistoryPath,
            fixture.SelectedPublicationRootPath,
            selectedBefore,
            fixture.FileSystem);

        var exception = Assert.Throws<CanonicalStateWriteException>(() =>
            PublishCachedAcceptedPlan(fixture));
        Assert.Equal(WoundHistoryState.HistoryPath, exception.RelativePath);
        Assert.Equal(
            $"Injected accepted-plan publication failure at '{WoundHistoryState.HistoryPath}'.",
            Assert.IsType<IOException>(exception.InnerException).Message);
        Assert.True(fault.Fired);
        fault.AssertFiredAfterAnEarlierPlanWrite();
        var selectedAfterImage = Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonObject>>(
            ReadRequiredProperty(flow.Plan, "OwnerCompanionAfterImages"))[
                fixture.SelectedPublicationRootPath];
        Assert.False(selectedBefore.AsSpan().SequenceEqual(
            JsonSerializer.SerializeToUtf8Bytes(selectedAfterImage)));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        fixture.AssertGovernedRootsMatchBeforeImages(beforeImages);
    }

    [Fact]
    public void PublicationPlan_ExactRetryReusesTheSameSealedAuthorityAndCachedPlanWithoutWriting()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "touched_player_active_skill_reads_exact_final_after_image", publication: true));

        var first = ComposePublicationFlow(fixture);
        var beforeRetry = fixture.CaptureGovernedPublicationBeforeImages();
        var retry = ComposePublicationFlow(fixture);
        Assert.Same(first.Plan, retry.Plan);
        Assert.Equal(
            ReadRequiredProperty(first.Plan, "PreparedPlanFingerprint"),
            ReadRequiredProperty(retry.Plan, "PreparedPlanFingerprint"));
        AssertEquivalentAuthority(first.Coordinates, retry.Coordinates);
        AssertEquivalentAuthority(first.Request, retry.Request);
        AssertEquivalentAuthority(first.Resolution, retry.Resolution);
        AssertEquivalentAuthority(first.SealedCurrent.Proof!, retry.SealedCurrent.Proof!);
        Assert.Equal(
            ReadRequiredProperty(first.Request, "RequestFingerprint"),
            ReadRequiredProperty(retry.Request, "RequestFingerprint"));
        Assert.Equal(
            ReadRequiredProperty(first.Resolution, "ResolutionAuthorityFingerprint"),
            ReadRequiredProperty(retry.Resolution, "ResolutionAuthorityFingerprint"));
        Assert.Equal(
            ReadRequiredProperty(first.Resolution, "ResultFingerprint"),
            ReadRequiredProperty(retry.Resolution, "ResultFingerprint"));
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var cachedRetry));
        Assert.True(cachedRetry.Success, DescribeIssues(cachedRetry.Issues));
        Assert.Same(first.Plan, cachedRetry.Plan);
        fixture.AssertGovernedRootsMatchBeforeImages(beforeRetry);
    }

    [Fact]
    public void PublicationPlan_ChangedExplicitNoOpConflictsWithTheCachedEmptyOperation()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "untouched_player_skill_reads_current_lease_bound_root", publication: true));
        var attempt = PreparePublicationAttempt(fixture);
        var first = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution,
            new GameResponse());
        AssertValidResultShell(first.IsValid, first.Issues, first.Value, "empty publication");
        var beforeConflict = fixture.CaptureGovernedPublicationBeforeImages();

        var conflicting = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution,
            new GameResponse { ActiveSkillChanges = Array.Empty<JsonElement>() });

        Assert.False(conflicting.IsValid);
        Assert.Null(conflicting.Value);
        Assert.Contains(conflicting.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_publication_conflict");
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var cached));
        Assert.Same(first.Value, cached.Plan);
        fixture.AssertGovernedRootsMatchBeforeImages(beforeConflict);
    }

    [Fact]
    public void PublicationPlan_MalformedSkillCommandRejectsBeforeCachingOrWriting()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "untouched_player_skill_reads_current_lease_bound_root", publication: true));
        var attempt = PreparePublicationAttempt(fixture);
        var before = fixture.CaptureGovernedPublicationBeforeImages();

        var rejected = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution,
            new GameResponse
            {
                ActiveSkillChanges = new[] { JsonSerializer.SerializeToElement("not-a-skill") }
            });

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Value);
        Assert.Contains(rejected.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_publication_skill_command_invalid");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        fixture.AssertGovernedRootsMatchBeforeImages(before);
    }

    [Fact]
    public void PublicationPlan_ExactRetryReturnsBeforeFreshSkillCapabilityReads()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "touched_player_active_skill_reads_exact_final_after_image", publication: true));
        var first = ComposePublicationFlow(fixture);
        fixture.MutatePersistedSelectedCapabilityAfterExport();
        var beforeRetry = fixture.CaptureGovernedPublicationBeforeImages();

        var retry = InvokeT070Publication(
            fixture,
            first.AcceptedState,
            first.Request,
            first.Resolution);

        AssertValidResultShell(retry.IsValid, retry.Issues, retry.Value, "exact cached retry");
        Assert.Same(first.Plan, retry.Value);
        fixture.AssertGovernedRootsMatchBeforeImages(beforeRetry);
    }

    [Fact]
    public void PublicationPlan_NpcActiveAndPassiveCommandsProduceOneNpcCoreAfterImage()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "touched_npc_active_skill_reads_exact_final_after_image", publication: true));
        var attempt = PreparePublicationAttempt(fixture);
        var proposal = fixture.CreateCombinedNpcSkillPublicationProposal();

        var result = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution,
            proposal);

        AssertValidResultShell(
            result.IsValid,
            result.Issues,
            result.Value,
            "combined NPC skill publication");
        fixture.AssertCombinedNpcSkillProjection(result.Value!);
    }

    [Fact]
    public void PublicationPlan_IdenticalMirroredNpcIsUpdatedInEveryCanonicalSection()
    {
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario(
                "touched_npc_active_skill_reads_exact_final_after_image",
                publication: true),
            mirrorNpc: true);
        var attempt = PreparePublicationAttempt(fixture);

        var result = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution);

        AssertValidResultShell(
            result.IsValid,
            result.Issues,
            result.Value,
            "mirrored NPC skill publication");
        fixture.AssertMirroredNpcProjection(result.Value!);
    }

    [Theory]
    [InlineData("divergent_cross_section")]
    [InlineData("same_section_duplicate")]
    public void PublicationPlan_InvalidNpcMirrorsRejectBeforeCachingOrWriting(string mutation)
    {
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario(
                "touched_npc_active_skill_reads_exact_final_after_image",
                publication: true),
            mirrorNpc: mutation == "divergent_cross_section");
        var attempt = PreparePublicationAttempt(fixture);
        fixture.MutateNpcMirror(mutation);
        var before = fixture.CaptureGovernedPublicationBeforeImages();

        var rejected = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution);

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Value);
        Assert.Contains(rejected.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_publication_npc_selector_ambiguous");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        fixture.AssertGovernedRootsMatchBeforeImages(before);
    }

    [Fact]
    public void PublicationPlan_MultipleNpcCommandsComposeIntoOneNpcCoreAfterImage()
    {
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario(
                "touched_npc_active_skill_reads_exact_final_after_image",
                publication: true),
            addSecondNpc: true);
        var attempt = PreparePublicationAttempt(fixture);

        var result = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution,
            fixture.CreateMultipleNpcSkillPublicationProposal());

        AssertValidResultShell(
            result.IsValid,
            result.Issues,
            result.Value,
            "multiple NPC skill publication");
        fixture.AssertMultipleNpcSkillProjection(result.Value!);
    }

    [Fact]
    public void PublicationPlan_UntouchedMirroredNpcExportsOneLogicalOwner()
    {
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario(
                "untouched_npc_skill_reads_current_lease_bound_root",
                publication: true),
            mirrorNpc: true);

        var flow = ComposePublicationFlow(fixture);

        Assert.Empty(Assert.IsType<AcceptedMechanicsPlan>(flow.Plan)
            .OwnerCompanionAfterImages);
    }

    [Fact]
    public void PublicationPlan_UnsupportedResponseFieldRejectsBeforeCachingOrWriting()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "untouched_player_skill_reads_current_lease_bound_root",
            publication: true));
        var attempt = PreparePublicationAttempt(fixture);
        var before = fixture.CaptureGovernedPublicationBeforeImages();

        var rejected = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution,
            new GameResponse { MoneyChange = 1 });

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Value);
        Assert.Contains(rejected.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_publication_response_unsupported");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        fixture.AssertGovernedRootsMatchBeforeImages(before);
    }

    [Fact]
    public void PublicationPlan_DetachedAfterImageMutationCannotAlterTheSealedCandidate()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "touched_player_active_skill_reads_exact_final_after_image",
            publication: true));
        var flow = ComposePublicationFlow(fixture);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(flow.Plan);
        var before = fixture.CaptureGovernedPublicationBeforeImages();
        var detached = plan.OwnerCompanionAfterImages[
            "game_state/player/skills_active.json"];
        detached.Clear();

        Assert.NotEmpty(plan.OwnerCompanionAfterImages[
            "game_state/player/skills_active.json"]);
        var published = PublishCachedAcceptedPlan(fixture);

        Assert.Same(plan, published);
        fixture.AssertPublicationPublishedExactSkillRoots(plan, before);
    }

    [Fact]
    public void PublicationAdmission_SwappedProjectionAuthorityRejectsAtTheGate()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "touched_player_active_skill_reads_exact_final_after_image",
            publication: true));
        var attempt = PreparePublicationAttempt(fixture);
        var composed = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution);
        AssertValidResultShell(
            composed.IsValid,
            composed.Issues,
            composed.Value,
            "sealed projection publication");
        var plan = Assert.IsType<AcceptedMechanicsPlan>(composed.Value);
        var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(plan.WoundStageBundle);
        var continuation = Assert.IsAssignableFrom<object>(
            bundle.PreparedPlan.TreatmentContinuationAuthority);
        var semanticFingerprint = Assert.IsType<string>(
            ReadRequiredProperty(continuation, "SemanticFingerprint"));
        var projectionField = Assert.IsAssignableFrom<FieldInfo>(
            continuation.GetType().GetField(
                "<SkillProjectionAuthority>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic));
        projectionField.SetValue(continuation, new object());

        var issues = MortalWoundTreatmentCapabilityAuthority.CandidateAdmissionGate.Validate(
            Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(attempt.AcceptedState),
            Assert.IsType<MortalWoundTreatmentAttemptRequest>(attempt.Request),
            Assert.IsType<MortalWoundTreatmentResolution>(attempt.Resolution),
            plan,
            semanticFingerprint,
            continuation);

        Assert.Contains(issues, static issue =>
            issue.Code is "mortal_wound_treatment_publication_provenance_mismatch" or
                "mortal_wound_treatment_publication_projection_mismatch");
    }

    [Fact]
    public void PublicationPlan_PresentEmptyNpcFieldIsATouchAndDiffersFromNull()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "untouched_npc_skill_reads_current_lease_bound_root",
            publication: true));
        var attempt = PreparePublicationAttempt(fixture);
        var beforeRoot = JsonNode.Parse(File.ReadAllText(fixture.FileSystem.ResolvePath(
            "game_state/npcs/npc_core.json")))!.AsObject();

        var explicitNoOp = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution,
            new GameResponse { NPCActiveSkillChanges = Array.Empty<JsonElement>() });

        AssertValidResultShell(
            explicitNoOp.IsValid,
            explicitNoOp.Issues,
            explicitNoOp.Value,
            "present-empty NPC skill publication");
        var plan = Assert.IsType<AcceptedMechanicsPlan>(explicitNoOp.Value);
        var after = Assert.Single(plan.OwnerCompanionAfterImages);
        Assert.Equal("game_state/npcs/npc_core.json", after.Key);
        Assert.True(JsonNode.DeepEquals(beforeRoot, after.Value));

        var nullOperation = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution,
            new GameResponse());
        Assert.False(nullOperation.IsValid);
        Assert.Contains(nullOperation.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_publication_conflict");
    }

    [Fact]
    public void PublicationPlan_UnselectedTouchedActorCannotSynthesizeTreatmentCapability()
    {
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario(
                "touched_npc_active_skill_reads_exact_final_after_image",
                publication: true),
            addSecondNpc: true);
        var attempt = PreparePublicationAttempt(fixture);
        var before = fixture.CaptureGovernedPublicationBeforeImages();

        var rejected = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution,
            fixture.CreateSynthesizingNpcSkillPublicationProposal());

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Value);
        Assert.Contains(rejected.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_capability_synthesized");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        fixture.AssertGovernedRootsMatchBeforeImages(before);
    }

    [Fact]
    public void PublicationExport_UntouchedNpcRejectsConfusableLiveOwnerAtCapabilityBoundary()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "untouched_npc_skill_reads_current_lease_bound_root",
            publication: true));
        var flow = ComposePublicationFlow(fixture);
        fixture.AddConfusableNpcSiblingAfterPublicationComposition();

        var rejected = InvokeCapabilityExporter(
            "ExportForPublication",
            flow.AcceptedState,
            flow.Coordinates,
            CapabilityRef,
            fixture.Scenario.ActorRole,
            flow.Plan);

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Proof);
        var issue = Assert.Single(rejected.Issues);
        Assert.Equal("mortal_wound_treatment_capability_source_ambiguous", issue.Code);
        Assert.Equal("treatmentCapability.source.skillId", issue.FilePath);
    }

    [Fact]
    public void PublicationAdmission_MutatedActorCatalogMapsCannotHideForgedCapabilityState()
    {
        using var fixture = CapabilityAuthorityFixture.Create(
            DescribeScenario(
                "touched_npc_active_skill_reads_exact_final_after_image",
                publication: true),
            addSecondNpc: true);
        var attempt = PreparePublicationAttempt(fixture);
        var composed = InvokeT070Publication(
            fixture,
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution,
            fixture.CreateMultipleNpcSkillPublicationProposal());
        AssertValidResultShell(
            composed.IsValid,
            composed.Issues,
            composed.Value,
            "sealed multiple-NPC publication");
        var plan = Assert.IsType<AcceptedMechanicsPlan>(composed.Value);
        var continuation = Assert.IsAssignableFrom<object>(
            Assert.IsType<AcceptedMechanicsWoundStageBundle>(plan.WoundStageBundle)
                .PreparedPlan.TreatmentContinuationAuthority);
        var semanticFingerprint = Assert.IsType<string>(
            ReadRequiredProperty(continuation, "SemanticFingerprint"));
        var projection = ReadRequiredProperty(continuation, "SkillProjectionAuthority");
        var actorBefore = Assert.IsAssignableFrom<IDictionary<string, JsonObject>>(
            projection.GetType().GetField(
                "_actorBeforeRoots",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(projection));
        var actorAfter = Assert.IsAssignableFrom<IDictionary<string, JsonObject>>(
            projection.GetType().GetField(
                "_actorAfterRoots",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(projection));
        var forgedRoot = fixture.CreateSynthesizedAssistantActiveRoot();
        actorBefore["npc:npc_publication_assistant_02:active"] =
            forgedRoot.DeepClone().AsObject();
        actorAfter["npc:npc_publication_assistant_02:active"] =
            forgedRoot.DeepClone().AsObject();

        var issues = MortalWoundTreatmentCapabilityAuthority.CandidateAdmissionGate.Validate(
            Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(attempt.AcceptedState),
            Assert.IsType<MortalWoundTreatmentAttemptRequest>(attempt.Request),
            Assert.IsType<MortalWoundTreatmentResolution>(attempt.Resolution),
            plan,
            semanticFingerprint,
            continuation);

        Assert.Contains(issues, static issue =>
            issue.Code is "mortal_wound_treatment_publication_provenance_mismatch" or
                "mortal_wound_treatment_publication_projection_mismatch");
    }

    private static CapabilityProofView InvokeExportCurrent(
        CapabilityAuthorityFixture fixture,
        CapabilityScenario scenario)
    {
        var acceptedState = BuildAcceptedState(fixture);
        var coordinates = CreateCoordinates(fixture, acceptedState);
        return InvokeCapabilityExporter(
            "ExportCurrent",
            acceptedState,
            coordinates,
            CapabilityRef,
            scenario.ActorRole);
    }

    private static CapabilityProofView InvokeExportForPublication(
        CapabilityAuthorityFixture fixture,
        CapabilityScenario scenario)
    {
        AssertNoDetachedPublicationSurface();
        Assert.True(scenario.ExpectedValid,
            "Direct five-argument publication export requires a T070-validated plan.");
        var flow = ComposePublicationFlow(fixture);
        var acceptedState = flow.AcceptedState;
        var coordinates = flow.Coordinates;
        // Publication always begins by sealing the same live canonical proof that the
        // guaranteed request carries.  The final root is then supplied only by T070's
        // accepted-plan pipeline; this test never writes an after-image or plan.
        var result = InvokeCapabilityExporter(
            "ExportForPublication",
            acceptedState,
            coordinates,
            CapabilityRef,
            scenario.ActorRole,
            flow.Plan);
        fixture.AssertPublicationExportReadsSelectedRoot(result);
        Assert.NotNull(flow.SealedCurrent.Proof);
        Assert.NotNull(result.Proof);
        AssertCapabilityProofBinding(result.Proof!, acceptedState, coordinates);
        AssertEquivalentAuthority(flow.SealedCurrent.Proof!, result.Proof!);
        return result;
    }

    private static PublicationFlow ComposePublicationFlow(
        CapabilityAuthorityFixture fixture,
        string operationKey = CapabilityAuthorityFixture.OperationKey)
    {
        var attempt = PreparePublicationAttempt(fixture, operationKey);
        var plan = ComposeT070PlanAndPeek(
            fixture,
            attempt.AcceptedState,
            attempt.AcceptedBinding,
            attempt.Request,
            attempt.Resolution);
        return new PublicationFlow(
            attempt.AcceptedState,
            attempt.Coordinates,
            attempt.SealedCurrent,
            attempt.Request,
            attempt.Resolution,
            plan);
    }

    private static PreparedPublicationAttempt PreparePublicationAttempt(
        CapabilityAuthorityFixture fixture,
        string operationKey = CapabilityAuthorityFixture.OperationKey)
    {
        var accepted = ExportAcceptedState(fixture);
        var acceptedState = RequireAcceptedState(accepted);
        var coordinates = CreateCoordinates(fixture, acceptedState, operationKey);
        var sealedCurrent = InvokeCapabilityExporter(
            "ExportCurrent",
            acceptedState,
            coordinates,
            CapabilityRef,
            fixture.Scenario.ActorRole);
        AssertCapabilityResult(sealedCurrent, fixture.Scenario with
        {
            ExpectedValid = true,
            ExpectedBoundary = CapabilityFailureBoundary.None,
            ExpectedCode = null,
            ExpectedPath = null
        });
        AssertCapabilityProofBinding(sealedCurrent.Proof!, acceptedState, coordinates);
        var request = PrepareSealedGuaranteedRequest(fixture, acceptedState, sealedCurrent, operationKey);
        var resolution = ResolveSealedGuaranteedAttempt(fixture, acceptedState, request);
        fixture.AssertPublicationFinalRootScenario();
        Assert.NotNull(accepted.Binding);
        return new PreparedPublicationAttempt(
            acceptedState,
            coordinates,
            sealedCurrent,
            request,
            resolution,
            accepted.Binding!);
    }

    private static string ComputePublicationSemanticFingerprint(
        PreparedPublicationAttempt attempt)
    {
        var method = Assert.Single(
            typeof(WoundAcceptedTurnPlanner).GetMethods(
                BindingFlags.Static | BindingFlags.NonPublic),
            static candidate =>
                candidate.Name == "ComputeTreatmentPublicationFingerprint");
        return Assert.IsType<string>(method.Invoke(null, new[]
        {
            attempt.AcceptedState,
            attempt.Request,
            attempt.Resolution
        }));
    }

    private static MortalWoundTreatmentResolution RecreateResolution(
        MortalWoundTreatmentResolution source,
        IReadOnlyList<MortalWoundTreatmentOutcomeIntent> outcomeIntents) =>
        MortalWoundTreatmentResolution.Create(
            source.Mode,
            source.Coordinates,
            source.AttemptDisposition,
            source.ResultCategory,
            source.SelectedOutcomeIndex,
            source.Interruption,
            source.DeclaredResult,
            outcomeIntents,
            source.CriticalReactionIntent,
            source.ConsumptionTrigger,
            source.CourseId,
            source.CourseMilestoneOrdinal,
            source.CourseDisposition,
            source.RequestAuthority,
            source.ModeEvidence,
            source.RouteFingerprint,
            source.RouteCompletion);

    private static WoundMaterializationEnvelope ReadPublishedPlayerWound(
        CapabilityAuthorityFixture fixture)
    {
        var carrier = JsonNode.Parse(File.ReadAllText(fixture.FileSystem.ResolvePath(
            WoundCarrierCatalog.PlayerPath)))!.AsObject();
        var woundNode = Assert.Single(carrier["activeWounds"]!.AsArray());
        var parsed = WoundMaterializationContract.Parse(
            woundNode!.ToJsonString(),
            WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        return Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
    }

    private static TypedResultView ComposeRejectedPublicationFlow(
        CapabilityAuthorityFixture fixture)
    {
        var accepted = ExportAcceptedState(fixture);
        var acceptedState = RequireAcceptedState(accepted);
        var coordinates = CreateCoordinates(fixture, acceptedState);
        var sealedCurrent = InvokeCapabilityExporter(
            "ExportCurrent",
            acceptedState,
            coordinates,
            CapabilityRef,
            fixture.Scenario.ActorRole);
        AssertCapabilityResult(sealedCurrent, fixture.Scenario with
        {
            ExpectedValid = true,
            ExpectedBoundary = CapabilityFailureBoundary.None,
            ExpectedCode = null,
            ExpectedPath = null
        });
        AssertCapabilityProofBinding(sealedCurrent.Proof!, acceptedState, coordinates);
        var request = PrepareSealedGuaranteedRequest(fixture, acceptedState, sealedCurrent);
        var resolution = ResolveSealedGuaranteedAttempt(fixture, acceptedState, request);
        fixture.AssertPublicationFinalRootScenario();
        Assert.NotNull(accepted.Binding);
        var result = InvokeT070Publication(
            fixture,
            acceptedState,
            request,
            resolution);
        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        return result;
    }

    private static AcceptedMechanicsPlan PublishCachedAcceptedPlan(
        CapabilityAuthorityFixture fixture)
    {
        fixture.ReleaseLeaseForTopLevelPublication();
        try
        {
            var result = AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    fixture.FileSystem,
                    new CanonicalStateNormalizer(
                        fixture.FileSystem,
                        NullLogger<CanonicalStateNormalizer>.Instance),
                    new ValidationService(
                        fixture.FileSystem,
                        NullLogger<ValidationService>.Instance),
                    new Dictionary<string, string>(StringComparer.Ordinal))
                .GetAwaiter()
                .GetResult();
            Assert.Empty(result.Issues);
            return result.MechanicsPlan ?? throw new Xunit.Sdk.XunitException(
                "The top-level accepted-turn transaction returned no cached publication plan.");
        }
        finally
        {
            fixture.ReacquireLeaseAfterTopLevelPublication();
        }
    }

    private static object BuildAcceptedState(CapabilityAuthorityFixture fixture)
    {
        var result = ExportAcceptedState(fixture);
        return RequireAcceptedState(result);
    }

    private static object RequireAcceptedState(AcceptedStateView result)
    {
        AssertValidResultShell(result.IsValid, result.Issues, result.Authority, "accepted state");
        return result.Authority!;
    }

    private static AcceptedStateView ExportAcceptedState(CapabilityAuthorityFixture fixture)
    {
        // T066 owns canonical/current-turn loading and derives both the accepted state
        // and its binding.  The fixture must never author either authority object.
        var acceptedStateAuthorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAcceptedStateAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(acceptedStateAuthorityType);
        Assert.Empty(acceptedStateAuthorityType!.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.DoesNotContain(
            acceptedStateAuthorityType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => candidate.Name == "ExportCurrent" &&
                                candidate.GetParameters().Any(static parameter =>
                                    parameter.ParameterType == typeof(JsonNode) ||
                                    parameter.ParameterType == typeof(JsonObject) ||
                                    parameter.ParameterType == typeof(JsonElement)));
        var treatmentAuthorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(treatmentAuthorityType);
        var parsedContext = ParseTreatmentContext(treatmentAuthorityType!, fixture.ContextRoot);
        AssertValidResultShell(
            parsedContext.IsValid,
            parsedContext.Issues,
            parsedContext.Value,
            "treatment context");
        var context = parsedContext.Value!;
        var method = Assert.Single(
            acceptedStateAuthorityType!.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate =>
                candidate.Name == "ExportCurrent" &&
                candidate.GetParameters().Length == 4);
        var parameters = method.GetParameters();
        Assert.Equal(typeof(FileSystemManager), parameters[0].ParameterType);
        Assert.Equal(fixture.Lease.GetType(), parameters[1].ParameterType);
        Assert.Equal(context.GetType(), parameters[2].ParameterType);
        Assert.Equal(typeof(string), parameters[3].ParameterType);

        var result = Invoke(method, new object[]
        {
            fixture.FileSystem,
            fixture.Lease,
            context,
            CapabilityAuthorityFixture.WoundId
        });
        var typedResult = ReadTypedResult(result, "Authority");
        if (typedResult.Value is not null)
            Assert.Equal("MortalWoundTreatmentAcceptedStateAuthority", typedResult.Value.GetType().Name);
        var binding = typedResult.Value is null
            ? null
            : ReadPropertyAllowingNull(typedResult.Value, "Binding");
        if (binding is not null)
            Assert.Equal("WoundAcceptedTurnBinding", binding.GetType().Name);
        return new AcceptedStateView(
            typedResult.IsValid,
            typedResult.Issues,
            typedResult.Value,
            binding);
    }

    private static void AssertContextRejected(
        CapabilityAuthorityFixture fixture,
        CapabilityScenario scenario)
    {
        var treatmentAuthorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(treatmentAuthorityType);
        var result = ParseTreatmentContext(treatmentAuthorityType!, fixture.ContextRoot);
        AssertInvalidResultShell(result.IsValid, result.Issues, result.Value, scenario, "context");
    }

    private static void AssertAcceptedStateRejected(
        CapabilityAuthorityFixture fixture,
        CapabilityScenario scenario)
    {
        var result = ExportAcceptedState(fixture);
        AssertInvalidResultShell(result.IsValid, result.Issues, result.Authority, scenario, "accepted state");
    }

    private static object CreateCoordinates(
        CapabilityAuthorityFixture fixture,
        object acceptedState,
        string operationKey = CapabilityAuthorityFixture.OperationKey)
    {
        var plannerType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentPlanner",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(plannerType);
        var method = Assert.Single(
            plannerType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate =>
                candidate.Name == "CreateAttemptCoordinates" &&
                candidate.GetParameters().Length == 5);
        var parameters = method.GetParameters();
        Assert.Equal(acceptedState.GetType(), parameters[0].ParameterType);
        Assert.Equal(typeof(WoundMaterializationEnvelope), parameters[1].ParameterType);
        Assert.Equal(typeof(string), parameters[2].ParameterType);
        Assert.Equal(typeof(string), parameters[3].ParameterType);
        Assert.Equal(typeof(string), parameters[4].ParameterType);

        var result = Invoke(method, new object[]
        {
            acceptedState,
            fixture.Before,
            operationKey,
            CapabilityAuthorityFixture.RouteId,
            CapabilityAuthorityFixture.EventRef
        });
        return ReadValidTypedResult(result, "Coordinates", "attempt coordinates");
    }

    private static object PrepareSealedGuaranteedRequest(
        CapabilityAuthorityFixture fixture,
        object acceptedState,
        CapabilityProofView sealedCurrent,
        string operationKey = CapabilityAuthorityFixture.OperationKey)
    {
        var plannerType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentPlanner",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(plannerType);
        var method = Assert.Single(plannerType.GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => candidate.Name == "PrepareGuaranteedRequest" &&
                                candidate.GetParameters().Length == 6);
        var parameters = method.GetParameters();
        Assert.Equal(acceptedState.GetType(), parameters[0].ParameterType);
        Assert.Equal(typeof(string), parameters[3].ParameterType);
        Assert.Equal(typeof(string), parameters[4].ParameterType);
        Assert.Equal(typeof(string), parameters[5].ParameterType);
        var history = fixture.ReadCurrentHistory();
        var result = Invoke(method, new object?[]
        {
            acceptedState,
            history,
            fixture.Before,
            operationKey,
            CapabilityAuthorityFixture.RouteId,
            CapabilityAuthorityFixture.EventRef
        });
        var request = ReadValidTypedResult(result, "Request", "sealed guaranteed request");
        Assert.Equal("guaranteed", Assert.IsType<string>(ReadRequiredProperty(request, "Mode")));
        var sealedProof = ReadRequiredProperty(request, "ModeAuthority");
        Assert.Equal(
            ReadRequiredProperty(sealedCurrent.Proof!, "ProofFingerprint"),
            ReadRequiredProperty(sealedProof, "ProofFingerprint"));
        Assert.Equal(
            ReadRequiredProperty(sealedCurrent.Proof!, "SourceSemanticFingerprint"),
            ReadRequiredProperty(sealedProof, "SourceSemanticFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(request, "RequestFingerprint"));
        return request;
    }

    private static object ResolveSealedGuaranteedAttempt(
        CapabilityAuthorityFixture fixture,
        object acceptedState,
        object request)
    {
        var plannerType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentPlanner",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(plannerType);
        var method = Assert.Single(plannerType.GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => candidate.Name == "CreateGuaranteedAttempt" &&
                                candidate.GetParameters().Length == 4);
        var result = Invoke(method, new object?[]
        {
            request,
            fixture.ReadCurrentHistory(),
            fixture.Before,
            acceptedState
        });
        Assert.Equal("MortalWoundTreatmentResolutionResult", result.GetType().Name);
        AssertClosedProperties(result, new[] { "Disposition", "Issues", "ReplayReceipt", "Resolution" });
        Assert.Equal("Resolved", Assert.IsType<string>(ReadRequiredProperty(result, "Disposition")));
        Assert.Empty(ReadEnumerableProperty(result, "Issues"));
        Assert.Null(ReadPropertyAllowingNull(result, "ReplayReceipt"));
        return ReadRequiredProperty(result, "Resolution");
    }

    private static object ComposeT070PlanAndPeek(
        CapabilityAuthorityFixture fixture,
        object acceptedState,
        object acceptedBinding,
        object request,
        object resolution)
    {
        var composed = InvokeT070Publication(
            fixture,
            acceptedState,
            request,
            resolution);
        AssertValidResultShell(
            composed.IsValid,
            composed.Issues,
            composed.Value,
            "T070 accepted publication");
        var publicationPlan = composed.Value!;
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var cachedBinding,
            out var cached));
        Assert.True(cached.Success, DescribeIssues(cached.Issues));
        Assert.Same(publicationPlan, cached.Plan);
        fixture.AssertCachedPublicationPlanBinding(cachedBinding, acceptedBinding);
        fixture.AssertPublicationPlanBindsExpectedRoots(publicationPlan);
        return publicationPlan;
    }

    private static TypedResultView InvokeT070Publication(
        CapabilityAuthorityFixture fixture,
        object acceptedState,
        object request,
        object resolution,
        GameResponse? suppliedProposal = null)
    {
        // T070 alone validates and normalizes the GM proposal, derives final
        // owner-companion after-images, and populates the common accepted-plan
        // cache. There is intentionally no test-built plan, planning input, final
        // root, or publication coordinator.
        var pipelineType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.WoundAcceptedTurnPlanner",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(pipelineType);
        var compose = Assert.Single(pipelineType.GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => candidate.Name == "ComposeMortalWoundTreatmentPublication" &&
                                candidate.GetParameters().Length == 6);
        var parameters = compose.GetParameters();
        Assert.Equal(typeof(FileSystemManager), parameters[0].ParameterType);
        Assert.Equal(typeof(FileSystemManager.CanonicalWriteLease), parameters[1].ParameterType);
        Assert.Equal(typeof(GameResponse), parameters[2].ParameterType);
        Assert.Equal(acceptedState.GetType(), parameters[3].ParameterType);
        Assert.Equal(request.GetType(), parameters[4].ParameterType);
        Assert.Equal(resolution.GetType(), parameters[5].ParameterType);
        var proposal = suppliedProposal ?? fixture.CreatePublicationProposal();
        if (suppliedProposal is null)
            fixture.AssertPublicationProposalIsNotFinalPlanAuthority(proposal);
        var beforeCompose = fixture.CaptureGovernedPublicationBeforeImages();
        var composed = Invoke(compose, new object?[]
        {
            fixture.FileSystem,
            fixture.Lease,
            proposal,
            acceptedState,
            request,
            resolution
        });
        fixture.AssertGovernedRootsMatchBeforeImages(beforeCompose);
        var result = ReadTypedResult(composed, "Plan");
        if (result.Value is not null)
        {
            Assert.NotSame(proposal, result.Value);
            Assert.NotEqual(proposal.GetType(), result.Value.GetType());
            fixture.AssertPublicationPlanUsesOnlyGovernedPaths(result.Value, beforeCompose);
        }
        return result;
    }

    private static CapabilityProofView InvokeCapabilityExporter(
        string methodName,
        object acceptedState,
        object coordinates,
        string capabilityRef,
        string actorRole,
        object? publicationPlan = null,
        bool includeNullPublicationPlan = false)
    {
        var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentCapabilityAuthority",
            throwOnError: false,
            ignoreCase: false);

        Assert.NotNull(authorityType);

        var parameterCount = publicationPlan is not null || includeNullPublicationPlan ? 5 : 4;
        var method = Assert.Single(
            authorityType.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
            candidate =>
                candidate.Name == methodName &&
                candidate.GetParameters().Length == parameterCount);

        var parameters = method.GetParameters();
        Assert.Equal(acceptedState.GetType(), parameters[0].ParameterType);
        Assert.Equal(coordinates.GetType(), parameters[1].ParameterType);
        Assert.Equal(typeof(string), parameters[2].ParameterType);
        Assert.Equal(typeof(string), parameters[3].ParameterType);
        if (parameterCount == 5)
        {
            Assert.Equal(typeof(AcceptedMechanicsPlan), parameters[4].ParameterType);
            if (publicationPlan is not null)
                Assert.Equal(publicationPlan.GetType(), parameters[4].ParameterType);
        }

        var arguments = parameterCount == 4
            ? new object?[] { acceptedState, coordinates, capabilityRef, actorRole }
            : new object?[] { acceptedState, coordinates, capabilityRef, actorRole, publicationPlan };
        var result = Invoke(method, arguments);
        Assert.Equal("MortalWoundTreatmentCapabilityProofResult", result.GetType().Name);
        var typedResult = ReadTypedResult(result, "Proof");
        return new CapabilityProofView(typedResult.IsValid, typedResult.Issues, typedResult.Value);
    }

    private static void AssertNoDetachedPublicationSurface()
    {
        var assembly = typeof(WoundMaterializationContract).Assembly;
        var capabilityAuthority = assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentCapabilityAuthority",
            throwOnError: false,
            ignoreCase: false);
        var acceptedStateAuthority = assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAcceptedStateAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(capabilityAuthority);
        Assert.NotNull(acceptedStateAuthority);

        foreach (var authority in new[] { capabilityAuthority!, acceptedStateAuthority! })
        {
            Assert.Empty(authority.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
            Assert.DoesNotContain(
                authority.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                static method => (method.Name == "ExportCurrent" || method.Name == "ExportForPublication") &&
                                 method.GetParameters().Any(IsDangerousDetachedPublicationParameter));
        }

        Assert.DoesNotContain(
            acceptedStateAuthority!.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static method => method.Name == "ExportCurrent" && method.GetParameters().Length != 4);
        Assert.DoesNotContain(
            capabilityAuthority!.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static method => method.Name == "ExportCurrent" && method.GetParameters().Length != 4);
        Assert.DoesNotContain(
            capabilityAuthority!.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static method => method.Name == "ExportForPublication" && method.GetParameters().Length != 5);
    }

    private static bool IsDangerousDetachedPublicationParameter(ParameterInfo parameter) =>
        parameter.ParameterType == typeof(JsonNode) ||
        parameter.ParameterType == typeof(JsonObject) ||
        parameter.ParameterType == typeof(JsonArray) ||
        parameter.ParameterType == typeof(JsonElement) ||
        parameter.Name?.Contains("intent", StringComparison.OrdinalIgnoreCase) == true ||
        parameter.Name?.Contains("afterImage", StringComparison.OrdinalIgnoreCase) == true ||
        parameter.Name?.Contains("fingerprint", StringComparison.OrdinalIgnoreCase) == true;

    private static JsonObject CreateCanonicalSkillFixture() => new()
    {
        ["realm"] = "mortal_world",
        ["player"] = CreateSkillOwner("player_current", includeCurrentMasteryLevel: false),
        ["npc"] = CreateSkillOwner("npc_field_medic_01", includeCurrentMasteryLevel: true)
    };

    private static JsonObject CreateSkillOwner(
        string ownerId,
        bool includeCurrentMasteryLevel)
    {
        var activeSkill = CreateSkill("active");
        if (includeCurrentMasteryLevel)
            activeSkill["currentMasteryLevel"] = 3;

        return new JsonObject
        {
            ["ownerId"] = ownerId,
            ["activeSkills"] = new JsonArray(activeSkill),
            ["passiveSkills"] = new JsonArray(CreateSkill("passive"))
        };
    }

    private static JsonObject CreateSkill(string kind)
    {
        // These are the smallest current production-valid materialization shapes from
        // ActorMaterializationContractTests.  The capability adapter must consume the
        // canonical skill rows, not a parallel test-only projection.
        var common = new JsonObject
        {
            ["skillId"] = kind == "active" ? SkillId : "skill_triage_passive_01",
            ["displayName"] = kind == "active" ? "Field Medicine" : "Triage Discipline",
            ["lifecycle"] = "active",
            ["active"] = true,
            ["tier"] = 3,
            ["skillName"] = kind == "active" ? "Field Medicine" : "Triage Discipline",
            ["skillDescription"] = kind == "active"
                ? "Provides precise field care under pressure."
                : "Retains a disciplined triage routine.",
            ["rarity"] = "Common"
        };
        if (kind == "active")
        {
            common["actionCost"] = "Main";
            common["combatEffect"] = new JsonObject
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
            };
        }
        else
        {
            common["type"] = "Utility";
            common["group"] = "Medicine";
            common["masteryLevel"] = 3;
            common["maxMasteryLevel"] = 5;
            common["structuredBonuses"] = null;
        }

        return common;
    }

    private static void ConfigureCapabilitySource(
        JsonObject fixture,
        CapabilityScenario scenario)
    {
        foreach (var owner in new[] { "player", "npc" })
        {
            foreach (var skillArray in new[] { "activeSkills", "passiveSkills" })
            {
                Skill(fixture, owner, skillArray).Remove("mortalWoundTreatmentCapabilities");
            }
        }

        var source = Skill(fixture, scenario.SourceOwner, scenario.SourceSkillArray);
        source["skillId"] = scenario.SkillId;
        AddCapability(source, CapabilityRef);
    }

    private static void AddCapability(JsonObject skill, string capabilityRef)
    {
        skill["mortalWoundTreatmentCapabilities"] = new JsonArray(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["capabilityRef"] = capabilityRef,
            ["woundDomain"] = "physical",
            ["minimumSeverityRank"] = 1,
            ["maximumSeverityRank"] = 4,
            ["operationLimits"] = new JsonObject
            {
                ["mayStabilize"] = true,
                ["maximumRecoveryPoints"] = 1,
                ["maximumSeverityReductionSteps"] = 1,
                ["removableComplicationKinds"] = new JsonArray("infection"),
                ["mayHealAtSeverityI"] = true,
                ["maximumCosmeticHealLegacies"] = 1,
                ["maximumMechanicalEffectHealLegacies"] = 1
            }
        });
    }

    private static JsonObject OtherSkill(JsonObject fixture, CapabilityScenario scenario) =>
        Skill(
            fixture,
            scenario.SourceOwner,
            scenario.SourceSkillArray == "activeSkills" ? "passiveSkills" : "activeSkills");

    private static void ApplyScenario(JsonObject fixture, CapabilityScenario scenario)
    {
        ConfigureCapabilitySource(fixture, scenario);
        var sourceSkill = Skill(fixture, scenario.SourceOwner, scenario.SourceSkillArray);
        var capability = Capability(sourceSkill);

        switch (scenario.Name)
        {
            case "idless_extension_bearing_skill_rejects":
                sourceSkill.Remove("skillId");
                break;
            case "duplicate_skill_id_across_active_and_passive_rejects":
                OtherSkill(fixture, scenario)["skillId"] = sourceSkill["skillId"]!.DeepClone();
                break;
            case "cross_root_player_npc_exact_skill_id_is_owner_scoped":
                Skill(fixture, "player", "passiveSkills")["skillId"] = sourceSkill["skillId"]!.DeepClone();
                break;
            case "cross_root_player_npc_exact_capability_ref_is_owner_scoped":
                AddCapability(Skill(fixture, "player", "passiveSkills"), CapabilityRef);
                break;
            case "cross_root_player_npc_confusable_skill_id_is_owner_scoped":
                Skill(fixture, "player", "passiveSkills")["skillId"] =
                    scenario.SkillId.Replace("i", "і", StringComparison.Ordinal);
                break;
            case "case_changed_skill_id_rejects":
                OtherSkill(fixture, scenario)["skillId"] = scenario.SkillId.ToUpperInvariant();
                break;
            case "unicode_confusable_skill_id_rejects":
                OtherSkill(fixture, scenario)["skillId"] =
                    scenario.SkillId.Replace("i", "і", StringComparison.Ordinal);
                break;
            case "duplicate_capability_ref_across_skill_kinds_rejects":
                AddCapability(OtherSkill(fixture, scenario), CapabilityRef);
                break;
            case "unicode_confusable_capability_ref_rejects":
                AddCapability(OtherSkill(fixture, scenario),
                    CapabilityRef.Replace("i", "і", StringComparison.Ordinal));
                break;
            case "inactive_skill_rejects":
                sourceSkill["active"] = false;
                break;
            case "retired_skill_rejects":
                sourceSkill["lifecycle"] = "retired";
                break;
            case "wrong_realm_rejects":
                fixture["realm"] = "chaos_sea";
                break;
            case "spiritual_domain_rejects":
                capability["woundDomain"] = "spiritual";
                break;
            case "unknown_domain_rejects":
                capability["woundDomain"] = "unknown";
                break;
            case "invalid_severity_envelope_rejects":
                capability["minimumSeverityRank"] = 4;
                capability["maximumSeverityRank"] = 1;
                break;
            case "open_extension_field_rejects":
                capability["callerMayOverride"] = true;
                break;
            case "open_operation_limits_field_rejects":
                Limits(capability)["callerMayOverride"] = true;
                break;
            case "aggregate_operation_limit_overflow_rejects":
                Limits(capability)["maximumCosmeticHealLegacies"] = 8;
                Limits(capability)["maximumMechanicalEffectHealLegacies"] = 1;
                break;
            case "all_zero_operation_limit_rejects":
                Limits(capability)["mayStabilize"] = false;
                Limits(capability)["maximumRecoveryPoints"] = 0;
                Limits(capability)["maximumSeverityReductionSteps"] = 0;
                Limits(capability)["removableComplicationKinds"] = new JsonArray();
                Limits(capability)["mayHealAtSeverityI"] = false;
                Limits(capability)["maximumCosmeticHealLegacies"] = 0;
                Limits(capability)["maximumMechanicalEffectHealLegacies"] = 0;
                break;
            case "source_semantic_change_changes_proof":
            case "changed_final_operation_limits_reject":
            case "changed_final_player_passive_operation_limits_reject":
            case "changed_final_npc_passive_operation_limits_reject":
                Limits(capability)["maximumRecoveryPoints"] = 2;
                break;
            case "touched_player_active_skill_reads_exact_final_after_image":
            case "touched_player_passive_skill_reads_exact_final_after_image":
            case "touched_npc_active_skill_reads_exact_final_after_image":
            case "touched_npc_passive_skill_reads_exact_final_after_image":
                sourceSkill["displayName"] = "Publication-only diagnostic wording";
                break;
            case "display_name_change_never_grants_authority":
                sourceSkill["displayName"] = "Unrelated wording only";
                break;
            case "removed_final_skill_rejects":
                fixture[scenario.SourceOwner]![scenario.SourceSkillArray] = new JsonArray();
                break;
            case "retired_final_skill_rejects":
                sourceSkill["lifecycle"] = "retired";
                break;
            case "changed_final_skill_id_rejects":
                sourceSkill["skillId"] = "skill_after_image_changed_01";
                sourceSkill["skillName"] = "After-image Field Medicine";
                break;
            case "changed_final_capability_ref_rejects":
                capability["capabilityRef"] = "after_image_changed_capability";
                break;
            case "changed_final_domain_rejects":
                capability["woundDomain"] = "spiritual";
                break;
            case "duplicate_or_confusable_final_skill_row_rejects":
                OtherSkill(fixture, scenario)["skillId"] = sourceSkill["skillId"]!.DeepClone();
                OtherSkill(fixture, scenario)["skillName"] =
                    "Publication sibling replacement";
                break;
            case "confusable_final_skill_sibling_rejects":
                OtherSkill(fixture, scenario)["skillId"] =
                    scenario.SkillId.Replace("i", "і", StringComparison.Ordinal);
                OtherSkill(fixture, scenario)["skillName"] =
                    "Publication sibling replacement";
                break;
            case "stale_final_source_rejects":
                sourceSkill["active"] = false;
                break;
        }
    }

    private static void AssertCanonicalFixtureShape(JsonObject fixture)
    {
        Assert.NotNull(fixture["realm"]);
        Assert.NotNull(fixture["player"]);
        Assert.NotNull(fixture["npc"]);
        Assert.NotNull(fixture["player"]!["activeSkills"]);
        Assert.NotNull(fixture["player"]!["passiveSkills"]);
        Assert.NotNull(fixture["npc"]!["activeSkills"]);
        Assert.NotNull(fixture["npc"]!["passiveSkills"]);
    }

    private static void AssertCapabilityResult(
        CapabilityProofView result,
        CapabilityScenario scenario)
    {
        Assert.Equal(scenario.ExpectedValid, result.IsValid);
        if (!scenario.ExpectedValid)
        {
            AssertInvalidResultShell(result.IsValid, result.Issues, result.Proof, scenario, "capability exporter");
            return;
        }

        Assert.Empty(result.Issues);
        Assert.NotNull(result.Proof);
        var proof = result.Proof!;
        AssertProofShape(proof, scenario);
        Assert.Equal(scenario.SkillId, Assert.IsType<string>(ReadRequiredProperty(proof, "SkillId")));
        Assert.Equal(scenario.SourceOwner, Assert.IsType<string>(ReadRequiredProperty(proof, "OwnerKind")));
        Assert.Equal(
            scenario.SourceOwner == "player" ? "player_current" : "npc_field_medic_01",
            Assert.IsType<string>(ReadRequiredProperty(proof, "OwnerId")));
        Assert.Equal(
            scenario.SourceSkillArray == "activeSkills" ? "active" : "passive",
            Assert.IsType<string>(ReadRequiredProperty(proof, "SkillKind")));
        var expectedSourcePath = scenario.SourceOwner switch
        {
            "npc" => "game_state/npcs/npc_core.json",
            "player" when scenario.SourceSkillArray == "activeSkills" =>
                "game_state/player/skills_active.json",
            "player" when scenario.SourceSkillArray == "passiveSkills" =>
                "game_state/player/skills_passive.json",
            _ => throw new Xunit.Sdk.XunitException(
                $"Unsupported capability provenance for {scenario.Name}.")
        };
        Assert.Equal(
            expectedSourcePath,
            Assert.IsType<string>(ReadRequiredProperty(proof, "SourcePath")));
    }

    private static void AssertProofShape(object proof, CapabilityScenario scenario)
    {
        Assert.False(proof is JsonNode or JsonElement or JsonDocument);
        var proofType = proof.GetType();
        Assert.Empty(proofType.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        var proofProperties = proofType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetIndexParameters().Length == 0)
            .ToArray();
        Assert.All(proofProperties, static property => Assert.False(property.CanWrite));
        var properties = proofProperties
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[]
            {
                "AcceptedStateFingerprint",
                "CapabilityRef",
                "ContextFingerprint",
                "CoordinatesFingerprint",
                "MaximumSeverityRank",
                "MinimumSeverityRank",
                "OperationLimits",
                "OwnerId",
                "OwnerKind",
                "ProofFingerprint",
                "SkillId",
                "SkillKind",
                "SnapshotToken",
                "SourcePath",
                "SourceSemanticFingerprint",
                "WoundDomain"
            },
            properties);
        Assert.Equal(CapabilityRef, Assert.IsType<string>(ReadRequiredProperty(proof, "CapabilityRef")));
        Assert.Equal("physical", Assert.IsType<string>(ReadRequiredProperty(proof, "WoundDomain")));
        Assert.Equal(1, Assert.IsType<int>(ReadRequiredProperty(proof, "MinimumSeverityRank")));
        Assert.Equal(4, Assert.IsType<int>(ReadRequiredProperty(proof, "MaximumSeverityRank")));
        var operationLimits = ReadRequiredProperty(proof, "OperationLimits");
        Assert.Empty(operationLimits.GetType().GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.All(
            operationLimits.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public),
            static property => Assert.False(property.CanWrite));
        var complicationKinds = ReadRequiredProperty(operationLimits, "RemovableComplicationKinds");
        if (complicationKinds is IList mutableKinds)
            Assert.ThrowsAny<Exception>(() => mutableKinds.Add("forged"));
        AssertFullOperationLimits(
            operationLimits,
            scenario.Name.StartsWith("untouched_", StringComparison.Ordinal) ||
            scenario.Name == "source_semantic_change_changes_proof" ? 2 : 1);
        AssertAuthorityFingerprint(ReadRequiredProperty(proof, "SourceSemanticFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(proof, "ProofFingerprint"));
        Assert.False(
            JsonSerializer.Serialize(proof, proof.GetType()).Contains("mortalWoundTreatmentCapabilities", StringComparison.Ordinal),
            $"{scenario.Name} leaked source JSON through the detached proof.");
    }

    private static void AssertCapabilityProofBinding(
        object proof,
        object acceptedState,
        object coordinates)
    {
        var binding = Assert.IsType<WoundAcceptedTurnBinding>(
            ReadAcceptedStateMember(acceptedState, "Binding"));
        Assert.Equal(
            binding.SessionId,
            Assert.IsType<string>(ReadRequiredProperty(coordinates, "SessionId")));
        Assert.Equal(
            binding.RequestId,
            Assert.IsType<string>(ReadRequiredProperty(coordinates, "RequestId")));
        Assert.Equal(
            binding.SnapshotToken,
            Assert.IsType<string>(ReadRequiredProperty(coordinates, "SnapshotToken")));
        Assert.Equal(
            binding.Turn,
            Convert.ToInt32(ReadRequiredProperty(coordinates, "Turn")));
        Assert.Equal(
            binding.Realm,
            Assert.IsType<string>(ReadRequiredProperty(coordinates, "Realm")));
        var acceptedEvent = Assert.Single(
            binding.AcceptedEvents,
            static value => value.EventRef == CapabilityAuthorityFixture.EventRef);
        Assert.Equal(CapabilityAuthorityFixture.EventKind, acceptedEvent.Kind);
        Assert.Equal(CapabilityAuthorityFixture.EventAuthorityId, acceptedEvent.AuthorityId);
        AssertAuthorityFingerprint(acceptedEvent.SemanticFingerprint);
        Assert.Equal(
            acceptedEvent.EventRef,
            Assert.IsType<string>(ReadRequiredProperty(coordinates, "EventRef")));
        Assert.Equal(
            acceptedEvent.Kind,
            Assert.IsType<string>(ReadRequiredProperty(coordinates, "EventKind")));
        Assert.Equal(
            acceptedEvent.AuthorityId,
            Assert.IsType<string>(ReadRequiredProperty(coordinates, "EventAuthorityId")));
        Assert.Equal(
            acceptedEvent.SemanticFingerprint,
            Assert.IsType<string>(ReadRequiredProperty(coordinates, "EventSemanticFingerprint")));
        Assert.Equal(
            binding.SnapshotToken,
            Assert.IsType<string>(ReadRequiredProperty(proof, "SnapshotToken")));
        foreach (var property in new[]
                 {
                     "ContextFingerprint",
                     "AcceptedStateFingerprint",
                     "CoordinatesFingerprint"
                 })
        {
            var expected = Assert.IsType<string>(ReadRequiredProperty(coordinates, property));
            AssertAuthorityFingerprint(expected);
            Assert.Equal(expected, Assert.IsType<string>(ReadRequiredProperty(proof, property)));
        }
    }

    private static void AssertEquivalentAuthority(object expected, object actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(
            JsonSerializer.Serialize(expected, expected.GetType()),
            JsonSerializer.Serialize(actual, actual.GetType()));
    }

    private static void AssertFullOperationLimits(object limits, int maximumRecoveryPoints)
    {
        AssertClosedProperties(limits, new[]
        {
            "MayHealAtSeverityI", "MayStabilize", "MaximumCosmeticHealLegacies",
            "MaximumMechanicalEffectHealLegacies", "MaximumRecoveryPoints",
            "MaximumSeverityReductionSteps", "RemovableComplicationKinds"
        });
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(limits, "MayStabilize")));
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(limits, "MayHealAtSeverityI")));
        Assert.Equal(maximumRecoveryPoints,
            Convert.ToInt32(ReadRequiredProperty(limits, "MaximumRecoveryPoints")));
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(limits, "MaximumSeverityReductionSteps")));
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(limits, "MaximumCosmeticHealLegacies")));
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(limits, "MaximumMechanicalEffectHealLegacies")));
        Assert.Equal(new[] { "infection" },
            ReadEnumerableProperty(limits, "RemovableComplicationKinds")
                .Select(Assert.IsType<string>));
    }

    private static void AssertScenarioSpecificCurrentSemantics(
        CapabilityAuthorityFixture fixture,
        CapabilityScenario scenario,
        CapabilityProofView result)
    {
        if (!scenario.ExpectedValid)
            return;

        switch (scenario.Name)
        {
            case "source_semantic_change_changes_proof":
            case "display_name_change_never_grants_authority":
                using (var baselineFixture = CapabilityAuthorityFixture.Create(
                           scenario with { Name = "baseline_current_source" }))
                {
                    var baseline = InvokeExportCurrent(baselineFixture, baselineFixture.Scenario);
                    AssertCapabilityResult(baseline, baselineFixture.Scenario);
                    Assert.NotNull(baseline.Proof);
                    Assert.NotNull(result.Proof);
                    var baselineSource = Assert.IsType<string>(ReadRequiredProperty(
                        baseline.Proof!, "SourceSemanticFingerprint"));
                    var currentSource = Assert.IsType<string>(ReadRequiredProperty(
                        result.Proof!, "SourceSemanticFingerprint"));
                    if (scenario.Name == "source_semantic_change_changes_proof")
                    {
                        Assert.NotEqual(baselineSource, currentSource);
                    }
                    else
                    {
                        Assert.Equal(baselineSource, currentSource);
                    }
                }
                break;
            case "proof_is_detached_and_immutable":
                Assert.NotNull(result.Proof);
                var beforeMutation = JsonSerializer.Serialize(result.Proof, result.Proof!.GetType());
                var operationLimits = ReadRequiredProperty(result.Proof, "OperationLimits");
                Assert.All(
                    operationLimits.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public),
                    static property => Assert.False(property.CanWrite));
                var complicationKinds = ReadRequiredProperty(
                    operationLimits,
                    "RemovableComplicationKinds");
                if (complicationKinds is IList mutableKinds)
                {
                    Assert.ThrowsAny<Exception>(() => mutableKinds.Add("forged"));
                }
                fixture.MutatePersistedSelectedSourceAfterExport();
                Assert.Equal(beforeMutation, JsonSerializer.Serialize(result.Proof, result.Proof.GetType()));
                break;
            case "ordinary_capability_projection_comes_from_same_skill":
            case "mastery_gate_remains_separate_skill_tier_requirement":
                AssertGuaranteedRequirementBundleDelegatesToUnchangedT060(fixture);
                break;
        }
    }

    private static void AssertGuaranteedRequirementBundleDelegatesToUnchangedT060(
        CapabilityAuthorityFixture fixture)
    {
        var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(authorityType);
        // Freeze the unchanged three-argument T060 target. Its typed context and
        // snapshot are exported by T066 accepted state; this test never reconstructs
        // either projection from a fixture-shaped JSON document.
        var resolver = Assert.Single(
            authorityType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => candidate.Name == "ResolveRequirements" &&
                                candidate.GetParameters().Length == 3);
        Assert.Equal(typeof(WoundTreatmentRoute), resolver.GetParameters()[0].ParameterType);

        var accepted = ExportAcceptedState(fixture);
        var acceptedState = RequireAcceptedState(accepted);
        var context = ReadAcceptedStateMember(acceptedState, "RequirementContext");
        var snapshot = ReadAcceptedStateMember(acceptedState, "RequirementSnapshot");
        Assert.Equal(resolver.GetParameters()[1].ParameterType, context.GetType());
        Assert.Equal(resolver.GetParameters()[2].ParameterType, snapshot.GetType());
        AssertDetachedRequirementProjection(context, "RequirementContext");
        AssertDetachedRequirementProjection(snapshot, "RequirementSnapshot");
        var route = Assert.Single(fixture.Before.Treatment.Routes);
        var direct = Invoke(resolver, new[] { (object)route, context, snapshot });
        AssertClosedProperties(direct, new[]
        {
            "Success", "Issues", "ResolvedRequirements", "AuthorityFingerprint"
        });
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(direct, "Success")));
        Assert.Empty(ReadEnumerableProperty(direct, "Issues"));
        AssertAuthorityFingerprint(ReadRequiredProperty(direct, "AuthorityFingerprint"));
        var directRows = ReadEnumerableProperty(direct, "ResolvedRequirements").ToArray();
        Assert.Equal(2, directRows.Length);

        var coordinates = CreateCoordinates(fixture, acceptedState);
        var bundleType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentRequirementAuthorityBundle",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(bundleType);
        var factory = Assert.Single(
            bundleType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => candidate.Name == "CreateForGuaranteed" &&
                                candidate.GetParameters().Length == 3);
        var parameters = factory.GetParameters();
        Assert.Equal(acceptedState.GetType(), parameters[0].ParameterType);
        Assert.Equal(coordinates.GetType(), parameters[1].ParameterType);
        Assert.Equal(typeof(WoundMaterializationEnvelope), parameters[2].ParameterType);
        var result = Invoke(factory, new[] { acceptedState, coordinates, (object)fixture.Before });
        var authority = ReadValidTypedResult(result, "Authority", "guaranteed requirement bundle");
        AssertClosedProperties(authority, new[]
        {
            "Mode", "ContextFingerprint", "AcceptedStateFingerprint", "RouteFingerprint", "CourseId",
            "CourseMilestoneOrdinal", "CourseCoordinateFingerprint", "CourseRequirementStatus",
            "InterruptionReason", "Scopes", "AuthorityFingerprint"
        });
        Assert.Equal("guaranteed", Assert.IsType<string>(ReadRequiredProperty(authority, "Mode")));
        Assert.Null(ReadPropertyAllowingNull(authority, "CourseId"));
        Assert.Null(ReadPropertyAllowingNull(authority, "CourseMilestoneOrdinal"));
        Assert.Null(ReadPropertyAllowingNull(authority, "CourseCoordinateFingerprint"));
        Assert.Null(ReadPropertyAllowingNull(authority, "CourseRequirementStatus"));
        Assert.Null(ReadPropertyAllowingNull(authority, "InterruptionReason"));
        AssertAuthorityFingerprint(ReadRequiredProperty(authority, "AuthorityFingerprint"));

        var scopes = ReadEnumerableProperty(authority, "Scopes").ToArray();
        var scope = Assert.Single(scopes);
        AssertClosedProperties(scope, new[]
        {
            "Scope", "CourseMilestoneOrdinal", "Status", "Bindings", "FailureWitnesses",
            "AuthorityFingerprint"
        });
        Assert.Equal("common", Assert.IsType<string>(ReadRequiredProperty(scope, "Scope")));
        Assert.Null(ReadPropertyAllowingNull(scope, "CourseMilestoneOrdinal"));
        Assert.Equal("Satisfied", Assert.IsType<string>(ReadRequiredProperty(scope, "Status")));
        Assert.Empty(ReadEnumerableProperty(scope, "FailureWitnesses"));
        AssertAuthorityFingerprint(ReadRequiredProperty(scope, "AuthorityFingerprint"));

        var bindings = ReadEnumerableProperty(scope, "Bindings").ToArray();
        Assert.Equal(2, bindings.Length);
        AssertGuaranteedBinding(
            bindings[0], directRows[0], fixture, requirementIndex: 0, "source_capability", CapabilityRef);
        AssertGuaranteedBinding(
            bindings[1], directRows[1], fixture, requirementIndex: 1, "skill_tier", fixture.Scenario.SkillId);
        AssertNoGuaranteeProofOrLimitSurface(authority);
    }

    private static void AssertGuaranteedBinding(
        object binding,
        object directRow,
        CapabilityAuthorityFixture fixture,
        int requirementIndex,
        string kind,
        string authorityRef)
    {
        AssertClosedProperties(binding, new[]
        {
            "RequirementIndex", "ResolvedRequirement", "SuccessWitness", "BindingFingerprint"
        });
        Assert.Equal(requirementIndex, Assert.IsType<int>(ReadRequiredProperty(binding, "RequirementIndex")));
        AssertAuthorityFingerprint(ReadRequiredProperty(binding, "BindingFingerprint"));

        var row = ReadRequiredProperty(binding, "ResolvedRequirement");
        AssertClosedProperties(row, new[]
        {
            "AuthorityFingerprint", "AuthorityRef", "CurrentState", "CurrentTier", "Kind", "LocationId",
            "MinimumTier", "OwnerId", "OwnerKind", "ProviderId", "ProviderKind", "Realm",
            "RequestedQuantity", "RequirementIndex", "TargetId", "TargetKind"
        });
        Assert.Equal(requirementIndex, Assert.IsType<int>(ReadRequiredProperty(row, "RequirementIndex")));
        Assert.Equal(kind, Assert.IsType<string>(ReadRequiredProperty(row, "Kind")));
        Assert.Equal(authorityRef, Assert.IsType<string>(ReadRequiredProperty(row, "AuthorityRef")));
        if (kind == "skill_tier")
        {
            Assert.Equal(3, Convert.ToInt32(ReadRequiredProperty(row, "CurrentTier")));
            Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(row, "MinimumTier")));
        }
        else
        {
            Assert.Null(ReadPropertyAllowingNull(row, "CurrentTier"));
            Assert.Null(ReadPropertyAllowingNull(row, "MinimumTier"));
        }
        Assert.Equal("mortal_world", Assert.IsType<string>(ReadRequiredProperty(row, "Realm")));
        Assert.Equal(fixture.Scenario.ProviderKind, ReadNullableStringProperty(row, "ProviderKind"));
        Assert.Equal(fixture.Scenario.ProviderId, ReadNullableStringProperty(row, "ProviderId"));
        Assert.Equal(fixture.Scenario.TargetKind, ReadNullableStringProperty(row, "TargetKind"));
        Assert.Equal(fixture.Scenario.TargetId, ReadNullableStringProperty(row, "TargetId"));
        Assert.Equal("loc_field_clinic_001", ReadNullableStringProperty(row, "LocationId"));
        AssertAuthorityFingerprint(ReadRequiredProperty(row, "AuthorityFingerprint"));
        AssertClosedProperties(directRow, new[]
        {
            "AuthorityFingerprint", "AuthorityRef", "CurrentState", "CurrentTier", "Kind", "LocationId",
            "MinimumTier", "OwnerId", "OwnerKind", "ProviderId", "ProviderKind", "Realm",
            "RequestedQuantity", "RequirementIndex", "TargetId", "TargetKind"
        });
        foreach (var property in new[]
                 {
                     "AuthorityFingerprint", "AuthorityRef", "CurrentState", "CurrentTier", "Kind", "LocationId",
                     "MinimumTier", "OwnerId", "OwnerKind", "ProviderId", "ProviderKind", "Realm",
                     "RequestedQuantity", "RequirementIndex", "TargetId", "TargetKind"
                 })
        {
            Assert.Equal(
                ReadPropertyAllowingNull(directRow, property),
                ReadPropertyAllowingNull(row, property));
        }
        if (kind == "skill_tier")
        {
            Assert.Equal(3, Convert.ToInt32(ReadRequiredProperty(directRow, "CurrentTier")));
            Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(directRow, "MinimumTier")));
        }
        else
        {
            Assert.Null(ReadPropertyAllowingNull(directRow, "CurrentTier"));
            Assert.Null(ReadPropertyAllowingNull(directRow, "MinimumTier"));
        }

        var witness = ReadRequiredProperty(binding, "SuccessWitness");
        Assert.False(witness is JsonNode or JsonDocument or JsonElement);
        foreach (var name in new[]
                 {
                     "Scope", "RequirementIndex", "Kind", "AuthorityRef", "SnapshotToken", "Realm",
                     "WitnessFingerprint"
                 })
            Assert.NotNull(witness.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public));
        Assert.Equal("common", Assert.IsType<string>(ReadRequiredProperty(witness, "Scope")));
        Assert.Equal(requirementIndex, Assert.IsType<int>(ReadRequiredProperty(witness, "RequirementIndex")));
        Assert.Equal(kind, Assert.IsType<string>(ReadRequiredProperty(witness, "Kind")));
        Assert.Equal(authorityRef, Assert.IsType<string>(ReadRequiredProperty(witness, "AuthorityRef")));
        Assert.Equal("mortal_world", Assert.IsType<string>(ReadRequiredProperty(witness, "Realm")));
        AssertAuthorityFingerprint(ReadRequiredProperty(witness, "WitnessFingerprint"));
        AssertNoGuaranteeProofOrLimitSurface(witness);
    }

    private static void AssertNoGuaranteeProofOrLimitSurface(object value)
    {
        Assert.DoesNotContain(
            value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name),
            static name => name.Contains("Proof", StringComparison.Ordinal) ||
                           name.Contains("Limit", StringComparison.Ordinal) ||
                           name.Contains("mortalWoundTreatmentCapabilities", StringComparison.Ordinal));
    }

    private static void AssertAuthorityFingerprint(object fingerprint) =>
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            Assert.IsType<string>(fingerprint)));

    private static void AssertClosedProperties(object value, IEnumerable<string> expected) =>
        Assert.Equal(
            expected.OrderBy(static name => name, StringComparer.Ordinal),
            value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));

    private static string? ReadNullableStringProperty(object instance, string name)
    {
        var value = ReadPropertyAllowingNull(instance, name);
        return value is null ? null : Assert.IsType<string>(value);
    }

    private static TypedResultView ParseTreatmentContext(Type authorityType, JsonObject contextRoot)
    {
        var parser = Assert.Single(
            authorityType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => candidate.Name == "ParseContext" &&
                                candidate.GetParameters().Length == 2 &&
                                candidate.GetParameters().All(static parameter => parameter.ParameterType == typeof(string)));
        var parsed = Invoke(parser, new object[] { contextRoot.ToJsonString(), "treatmentContext" });
        return ReadTypedResult(parsed, "Context");
    }

    private static object ReadValidTypedResult(object result, string valueProperty, string boundary)
    {
        var parsed = ReadTypedResult(result, valueProperty);
        AssertValidResultShell(parsed.IsValid, parsed.Issues, parsed.Value, boundary);
        return parsed.Value!;
    }

    private static TypedResultView ReadTypedResult(object result, string valueProperty)
    {
        var properties = result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetIndexParameters().Length == 0)
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[] { "IsValid", "Issues", valueProperty }.OrderBy(static name => name, StringComparer.Ordinal),
            properties);
        var isValid = Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid"));
        var issues = ReadEnumerableProperty(result, "Issues")
            .Select(Assert.IsType<ValidationIssue>)
            .ToArray();
        var value = ReadPropertyAllowingNull(result, valueProperty);
        return new TypedResultView(isValid, issues, value);
    }

    private static void AssertValidResultShell(
        bool isValid,
        IReadOnlyList<ValidationIssue> issues,
        object? value,
        string boundary)
    {
        Assert.True(isValid, $"{boundary} failed: {DescribeIssues(issues)}");
        Assert.Empty(issues);
        Assert.NotNull(value);
    }

    private static void AssertInvalidResultShell(
        bool isValid,
        IReadOnlyList<ValidationIssue> issues,
        object? value,
        CapabilityScenario scenario,
        string actualBoundary)
    {
        Assert.NotEqual(CapabilityFailureBoundary.None, scenario.ExpectedBoundary);
        Assert.Equal(scenario.ExpectedBoundary.ToString(), actualBoundary switch
        {
            "context" => CapabilityFailureBoundary.Context.ToString(),
            "accepted state" => CapabilityFailureBoundary.AcceptedState.ToString(),
            "capability exporter" => CapabilityFailureBoundary.Exporter.ToString(),
            "T070 publication" => CapabilityFailureBoundary.Exporter.ToString(),
            _ => actualBoundary
        });
        Assert.False(isValid);
        Assert.Null(value);
        Assert.NotEmpty(issues);
        Assert.All(issues, static issue =>
        {
            Assert.False(string.IsNullOrWhiteSpace(issue.Code));
            Assert.False(string.IsNullOrWhiteSpace(issue.FilePath));
        });
        if (scenario.ExpectedCode is not null)
        {
            Assert.NotNull(scenario.ExpectedPath);
            var issue = Assert.Single(issues);
            Assert.Equal(scenario.ExpectedCode, issue.Code);
            Assert.Equal(scenario.ExpectedPath, issue.FilePath);
        }
        AssertFrozenIssues(issues);
    }

    private static void AssertFrozenIssues(IReadOnlyList<ValidationIssue> issues)
    {
        Assert.DoesNotContain(
            issues.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public),
            static property => property.CanWrite);
        if (issues is IList mutableIssues)
            Assert.ThrowsAny<Exception>(() => mutableIssues.Add(null));
        else
            Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(issues);
    }

    private static object Invoke(MethodInfo method, object?[] arguments)
    {
        try
        {
            var value = method.Invoke(null, arguments);
            Assert.NotNull(value);
            return value;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static object? ReadPropertyAllowingNull(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property!.GetValue(instance);
    }

    private static object ReadRequiredProperty(object instance, string propertyName)
    {
        var value = ReadPropertyAllowingNull(instance, propertyName);
        Assert.NotNull(value);
        return value;
    }

    private static IEnumerable<object> ReadEnumerableProperty(object instance, string propertyName) =>
        Assert.IsAssignableFrom<IEnumerable>(ReadRequiredProperty(instance, propertyName))
            .Cast<object>();

    private static object ReadAcceptedStateMember(object acceptedState, string propertyName)
    {
        var value = ReadRequiredProperty(acceptedState, propertyName);
        Assert.False(value is JsonNode or JsonElement or JsonDocument or string);
        return value;
    }

    private static void AssertDetachedRequirementProjection(object projection, string propertyName)
    {
        Assert.False(projection is JsonNode or JsonElement or JsonDocument or string);
        Assert.DoesNotContain(
            projection.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            static property => property.PropertyType == typeof(JsonObject) ||
                               property.PropertyType == typeof(JsonArray) ||
                               property.Name.Contains("Proof", StringComparison.Ordinal) ||
                               property.Name.Contains("Limit", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(propertyName));
    }

    private static CapabilityScenario DescribeScenario(string name, bool publication)
    {
        var (owner, skillArray) = name switch
        {
            "player_active_skill_exports_physical_capability" or
            "untouched_player_skill_reads_current_lease_bound_root" or
            "untouched_player_skill_preserves_unrelated_deterioration_anchor" or
            "touched_player_active_skill_reads_exact_final_after_image" or
            "changed_final_skill_id_rejects" or
            "duplicate_or_confusable_final_skill_row_rejects" or
            "confusable_final_skill_sibling_rejects" => ("player", "activeSkills"),
            "player_passive_skill_exports_physical_capability" or
            "untouched_player_passive_skill_reads_current_lease_bound_root" or
            "touched_player_passive_skill_reads_exact_final_after_image" or
            "changed_final_player_passive_operation_limits_reject" => ("player", "passiveSkills"),
            "target_owned_combatant_requires_promotion" => ("player", "activeSkills"),
            "npc_passive_skill_exports_physical_capability" or
            "untouched_npc_passive_skill_reads_current_lease_bound_root" or
            "touched_npc_passive_skill_reads_exact_final_after_image" or
            "changed_final_npc_passive_operation_limits_reject" => ("npc", "passiveSkills"),
            _ => ("npc", "activeSkills")
        };
        var actorRole = name.Contains("target_owned", StringComparison.Ordinal)
            ? "target"
            : "provider";
        // The accepted context always starts with the real provider binding.  The
        // wrong-owner row changes only the live canonical NPC root after that state is
        // sealed, so it cannot accidentally test an earlier context rejection.
        var providerKind = owner;
        var providerId = providerKind == "player" ? "player_current" : "npc_field_medic_01";
        var targetKind = name.Contains("combatant_member", StringComparison.Ordinal)
            ? "combatant_member"
            : name.Contains("combatant", StringComparison.Ordinal)
                ? "combatant"
            : "player";
        var targetId = targetKind == "combatant_member"
            ? "combatant_member_wounded_01"
            : targetKind == "combatant" ? "combatant_wounded_01" : "player_current";
        if (name == "wrong_role_rejects")
            actorRole = "target";
        var expectedBoundary = name switch
        {
            "wrong_realm_rejects" => CapabilityFailureBoundary.Context,
            "idless_extension_bearing_skill_rejects" or
            "duplicate_skill_id_across_active_and_passive_rejects" or
            "case_changed_skill_id_rejects" or
            "unicode_confusable_skill_id_rejects" or
            "duplicate_capability_ref_across_skill_kinds_rejects" or
            "unicode_confusable_capability_ref_rejects" or
            "spiritual_domain_rejects" or
            "unknown_domain_rejects" or
            "invalid_severity_envelope_rejects" or
            "open_extension_field_rejects" or
            "open_operation_limits_field_rejects" or
            "aggregate_operation_limit_overflow_rejects" or
            "all_zero_operation_limit_rejects" => CapabilityFailureBoundary.AcceptedState,
            "inactive_skill_rejects" or
            "retired_skill_rejects" or
            "wrong_owner_rejects" or
            "wrong_role_rejects" or
            "target_owned_combatant_requires_promotion" or
            "removed_final_skill_rejects" or
            "retired_final_skill_rejects" or
            "changed_final_skill_id_rejects" or
            "changed_final_capability_ref_rejects" or
            "changed_final_domain_rejects" or
            "changed_final_operation_limits_reject" or
            "changed_final_player_passive_operation_limits_reject" or
            "changed_final_npc_passive_operation_limits_reject" or
            "duplicate_or_confusable_final_skill_row_rejects" or
            "confusable_final_skill_sibling_rejects" or
            "stale_final_source_rejects" => CapabilityFailureBoundary.Exporter,
            _ => CapabilityFailureBoundary.None
        };
        (string? expectedCode, string? expectedPath) = name switch
        {
            "target_owned_combatant_requires_promotion" =>
                ("mortal_wound_treatment_capability_actor_promotion_required", "treatmentCapability.target"),
            "wrong_role_rejects" =>
                ("mortal_wound_treatment_capability_binding_mismatch", "treatmentCapability.actorRole"),
            "wrong_owner_rejects" =>
                ("mortal_wound_treatment_capability_binding_mismatch", "treatmentCapability.sourceOwner"),
            "removed_final_skill_rejects" =>
                ("mortal_wound_treatment_capability_source_missing", "treatmentCapability.source"),
            "inactive_skill_rejects" or
            "retired_skill_rejects" or
            "retired_final_skill_rejects" or
            "stale_final_source_rejects" =>
                ("mortal_wound_treatment_capability_source_inactive", "treatmentCapability.source.lifecycle"),
            "duplicate_or_confusable_final_skill_row_rejects" or
            "confusable_final_skill_sibling_rejects" =>
                ("mortal_wound_treatment_capability_source_ambiguous", "treatmentCapability.source.skillId"),
            "changed_final_skill_id_rejects" or
            "changed_final_capability_ref_rejects" or
            "changed_final_domain_rejects" or
            "changed_final_operation_limits_reject" or
            "changed_final_player_passive_operation_limits_reject" or
            "changed_final_npc_passive_operation_limits_reject" =>
                ("mortal_wound_treatment_capability_publication_mismatch", "treatmentCapability.publicationPlan"),
            _ => (null, null)
        };
        return new CapabilityScenario(
            name,
            owner,
            skillArray,
            owner == "player" && skillArray == "activeSkills"
                ? SkillId
                : $"skill_{owner}_{(skillArray == "activeSkills" ? "active" : "passive")}_medicine_01",
            actorRole,
            providerKind,
            providerId,
            targetKind,
            targetId,
            expectedBoundary == CapabilityFailureBoundary.None,
            publication,
            expectedBoundary,
            expectedCode,
            expectedPath);
    }

    private static JsonObject Skill(JsonObject fixture, string owner, string skillKind) =>
        Assert.IsType<JsonObject>(Assert.Single(
            fixture[owner]![skillKind]!.AsArray()));

    private static JsonObject Capability(JsonObject skill) =>
        Assert.IsType<JsonObject>(Assert.Single(
            skill["mortalWoundTreatmentCapabilities"]!.AsArray()));

    private static JsonObject Limits(JsonObject capability) =>
        Assert.IsType<JsonObject>(capability["operationLimits"]);

    private static string DescribeIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(" | ", issues.Select(issue =>
            $"{issue.Code}@{issue.FilePath}: {issue.Message}"));

    private sealed record CapabilityProofView(
        bool IsValid,
        IReadOnlyList<ValidationIssue> Issues,
        object? Proof);

    private sealed record PublicationFlow(
        object AcceptedState,
        object Coordinates,
        CapabilityProofView SealedCurrent,
        object Request,
        object Resolution,
        object Plan);

    private sealed record PreparedPublicationAttempt(
        object AcceptedState,
        object Coordinates,
        CapabilityProofView SealedCurrent,
        object Request,
        object Resolution,
        object AcceptedBinding);

    private sealed record PublicationBeforeImage(bool Existed, byte[]? Bytes);

    private sealed class PublicationFailureInjection
    {
        private string? _path;
        private string? _earlierChangedPath;
        private byte[]? _earlierBeforeBytes;
        private FileSystemManager? _fileSystem;

        private readonly List<string> _observedPaths = [];

        internal bool Fired { get; private set; }

        internal bool ObservedEarlierCanonicalChange { get; private set; }

        internal int ObservedMutationCount => _observedPaths.Count;

        internal void ResetObservation()
        {
            _path = null;
            _earlierChangedPath = null;
            _earlierBeforeBytes = null;
            _fileSystem = null;
            Fired = false;
            ObservedEarlierCanonicalChange = false;
            _observedPaths.Clear();
        }

        internal void ArmAfterCanonicalChange(
            string path,
            string earlierChangedPath,
            byte[] earlierBeforeBytes,
            FileSystemManager fileSystem)
        {
            _path = path;
            _earlierChangedPath = earlierChangedPath;
            _earlierBeforeBytes = earlierBeforeBytes.ToArray();
            _fileSystem = fileSystem;
            Fired = false;
            ObservedEarlierCanonicalChange = false;
            _observedPaths.Clear();
        }

        internal Task BeforeCanonicalMutationAsync(string path)
        {
            _observedPaths.Add(path);
            if (!Fired && string.Equals(path, _path, StringComparison.Ordinal))
            {
                Fired = true;
                var earlierChangedPath = Assert.IsType<string>(_earlierChangedPath);
                var earlierBeforeBytes = Assert.IsType<byte[]>(_earlierBeforeBytes);
                var fileSystem = Assert.IsType<FileSystemManager>(_fileSystem);
                var earlierPhysicalPath = fileSystem.ResolvePath(earlierChangedPath);
                ObservedEarlierCanonicalChange = File.Exists(earlierPhysicalPath) &&
                    !earlierBeforeBytes.AsSpan().SequenceEqual(
                        File.ReadAllBytes(earlierPhysicalPath));
                return Task.FromException(new IOException(
                    $"Injected accepted-plan publication failure at '{path}'."));
            }

            return Task.CompletedTask;
        }

        internal void AssertFiredAfterAnEarlierPlanWrite()
        {
            Assert.True(Fired);
            Assert.NotNull(_path);
            Assert.NotNull(_earlierChangedPath);
            var firedIndex = _observedPaths.FindIndex(path =>
                string.Equals(path, _path, StringComparison.Ordinal));
            Assert.True(firedIndex > 0,
                "The injected failure must occur after at least one publication mutation.");
            Assert.Contains(
                _observedPaths.Take(firedIndex),
                path => string.Equals(path, _earlierChangedPath, StringComparison.Ordinal));
            Assert.True(
                ObservedEarlierCanonicalChange,
                $"Canonical root '{_earlierChangedPath}' had not changed before the later failure.");
        }
    }

    private sealed record TypedResultView(
        bool IsValid,
        IReadOnlyList<ValidationIssue> Issues,
        object? Value);

    private sealed record AcceptedStateView(
        bool IsValid,
        IReadOnlyList<ValidationIssue> Issues,
        object? Authority,
        object? Binding);

    public enum CapabilityFailureBoundary
    {
        None,
        Context,
        AcceptedState,
        Exporter
    }

    public sealed record CapabilityScenario(
        string Name,
        string SourceOwner,
        string SourceSkillArray,
        string SkillId,
        string ActorRole,
        string ProviderKind,
        string ProviderId,
        string TargetKind,
        string TargetId,
        bool ExpectedValid,
        bool Publication,
        CapabilityFailureBoundary ExpectedBoundary,
        string? ExpectedCode,
        string? ExpectedPath);

    private sealed class CapabilityAuthorityFixture : IDisposable
    {
        private CapabilityAuthorityFixture(
            string root,
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease,
            WoundMaterializationEnvelope before,
            LiveTurnPreparationResult preparedTurn,
            JsonObject contextRoot,
            CapabilityScenario scenario,
            JsonObject currentSource,
            JsonObject finalSource)
        {
            Root = root;
            FileSystem = fileSystem;
            _lease = lease;
            Before = before;
            PreparedTurn = preparedTurn;
            ContextRoot = contextRoot;
            Scenario = scenario;
            CurrentSource = currentSource;
            FinalSource = finalSource;
        }

        internal const string WoundId = "wound_test_torn_side";
        internal const string RouteId = "guaranteed_v1";
        internal const string EventRef = "turn_42:accepted_effect";
        internal const string EventKind = "accepted_turn";
        internal const string EventAuthorityId = "turn_42";
        internal const string OperationKey = "capability_authority_operation_001";
        internal const string UnrelatedEffectId = "effect_t070_unrelated_focus";

        // Test-owned inventory of the complete canonical transaction scope. Neither
        // the plan nor the production rollback list is allowed to choose this oracle.
        private static readonly string[] GovernedPublicationPaths =
        {
            "game_state/combat/allies.json",
            "game_state/combat/enemies.json",
            "game_state/control/mortal_bootstrap_scaffold.json",
            "game_state/control/pending_craft_request.json",
            "game_state/control/pending_effect_resolutions.json",
            "game_state/control/pending_mortal_wound_occurrences.json",
            "game_state/control/pending_npc_trade_inventory_requests.json",
            "game_state/control/pending_wound_resolutions.json",
            "game_state/control/progression_report.json",
            "game_state/control/progression_schedule.json",
            "game_state/effects/effect_commands.json",
            "game_state/effects/effect_identity_index.json",
            "game_state/factions/faction_chronicles.json",
            "game_state/factions/faction_core.json",
            "game_state/factions/faction_custom.json",
            "game_state/factions/faction_projects.json",
            "game_state/factions/faction_resources.json",
            "game_state/factions/faction_structure.json",
            "game_state/inventory/item_bonds.json",
            "game_state/inventory/item_identity_index.json",
            "game_state/inventory/item_removals.json",
            "game_state/inventory/item_text_updates.json",
            "game_state/inventory/items.json",
            "game_state/inventory/recipes.json",
            "game_state/meta/abode_power_journal.json",
            "game_state/meta/achievements.json",
            "game_state/meta/afterlife_active_threats.json",
            "game_state/meta/afterlife_entity_profiles.json",
            "game_state/meta/afterlife_spiritual_conflict_state.json",
            "game_state/meta/afterlife_story_outline.json",
            "game_state/meta/chaos_sea_guardian_politics.json",
            "game_state/meta/character_chronicle.json",
            "game_state/meta/guardian_abode_residents.json",
            "game_state/meta/guardian_project_journal.json",
            "game_state/meta/guardian_projects.json",
            "game_state/meta/guardian_social_journal.json",
            "game_state/meta/guardian_thought_journal.json",
            "game_state/meta/guardians.json",
            "game_state/meta/main_story_saref_state.json",
            "game_state/meta/shining_abode_state.json",
            "game_state/meta/soul_state.json",
            "game_state/misc/characteristics.json",
            "game_state/misc/vehicles.json",
            "game_state/npcs/item_journals.json",
            "game_state/npcs/npc_core.json",
            "game_state/npcs/npc_effects.json",
            "game_state/npcs/npc_interaction_journal.json",
            "game_state/npcs/npc_inventory.json",
            "game_state/npcs/npc_journals.json",
            "game_state/npcs/npc_wounds.json",
            "game_state/player/computed_characteristics.json",
            "game_state/player/effects.json",
            "game_state/player/skills_active.json",
            "game_state/player/skills_passive.json",
            "game_state/player/wounds.json",
            "game_state/quests/quest_history.json",
            "game_state/quests/regular_quests.json",
            "game_state/quests/soul_quests.json",
            "game_state/resources/resource_commands.json",
            "game_state/resources/resource_definitions.json",
            "game_state/resources/resource_history.json",
            "game_state/resources/resource_owner_authority.json",
            "game_state/resources/resource_state.json",
            "game_state/world/current_location.json",
            "game_state/world/location_identity_index.json",
            "game_state/world/location_storage_contents.json",
            "game_state/world/rival_soul_arcs.json",
            "game_state/world/world_events.json",
            "game_state/world/world_map.json",
            "game_state/wounds/wound_commands.json",
            "game_state/wounds/wound_history.json",
            "game_state/wounds/wound_identity_index.json",
            "game_state/wounds/wound_opportunity_receipts.json",
            "lore/codex_entries.json",
            "output/debug_logs.json",
            "output/interface_updates.json",
            "output/narrative_response.json"
        };

        private FileSystemManager.CanonicalWriteLease? _lease;

        internal string Root { get; }
        internal FileSystemManager FileSystem { get; }
        internal FileSystemManager.CanonicalWriteLease Lease =>
            _lease ?? throw new InvalidOperationException(
                "The fixture canonical write lease is temporarily released.");
        internal WoundMaterializationEnvelope Before { get; }
        internal LiveTurnPreparationResult PreparedTurn { get; }
        internal JsonObject ContextRoot { get; }
        internal CapabilityScenario Scenario { get; }
        internal JsonObject CurrentSource { get; }
        internal JsonObject FinalSource { get; }

        internal static CapabilityAuthorityFixture Create(
            CapabilityScenario scenario,
            FileSystemManagerHooks? hooks = null,
            bool mirrorNpc = false,
            bool addSecondNpc = false)
        {
            var root = Path.Combine(Path.GetTempPath(), "boe-capability-authority-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var fileSystem = hooks is null
                ? new FileSystemManager(root, NullLogger<FileSystemManager>.Instance)
                : new FileSystemManager(
                    root,
                    NullLogger<FileSystemManager>.Instance,
                    PhysicalLoadTransactionOperations.Instance,
                    hooks);
            fileSystem.EnsureDirectoryStructure();
            Directory.CreateDirectory(Path.GetDirectoryName(
                fileSystem.ResolvePath(WoundIdentityState.StatePath))!);
            Directory.CreateDirectory(Path.GetDirectoryName(
                fileSystem.ResolvePath(WoundHistoryState.HistoryPath))!);
            Directory.CreateDirectory(Path.GetDirectoryName(
                fileSystem.ResolvePath(LiveTurnPreparationService.TurnRequestPath))!);

            var currentSource = CreateCanonicalSkillFixture();
            ApplyScenario(currentSource, scenario with { Name = "baseline_current_source" });
            var finalSource = scenario.Publication
                ? currentSource.DeepClone().AsObject()
                : currentSource;
            if (scenario.Publication)
            {
                ApplyScenario(finalSource, scenario);
                if (scenario.Name.StartsWith("untouched_", StringComparison.Ordinal))
                {
                    // The untouched plan deliberately contains no skill-root after-image.
                    // A distinct still-valid live root proves that ExportForPublication
                    // must read the lease-bound canonical source rather than any final root.
                    Limits(Capability(Skill(
                        currentSource,
                        scenario.SourceOwner,
                        scenario.SourceSkillArray)))["maximumRecoveryPoints"] = 2;
                }
            }
            else if (scenario.ExpectedBoundary != CapabilityFailureBoundary.Exporter)
                ApplyScenario(currentSource, scenario);
            if (scenario.Publication)
            {
                MakeUnselectedSkillIdsOwnerUnique(
                    currentSource,
                    scenario.SourceOwner);
                MakeUnselectedSkillIdsOwnerUnique(
                    finalSource,
                    scenario.SourceOwner);
            }
            AssertCanonicalFixtureShape(currentSource);
            AssertCanonicalFixtureShape(finalSource);
            WriteCanonicalSkillSources(fileSystem, currentSource);
            if (mirrorNpc)
            {
                var npcPath = fileSystem.ResolvePath("game_state/npcs/npc_core.json");
                var npcRoot = JsonNode.Parse(File.ReadAllText(npcPath))!.AsObject();
                var actor = Assert.IsType<JsonObject>(Assert.Single(
                    Assert.IsType<JsonArray>(npcRoot["NPCsInScene"])));
                npcRoot["UpdateNPCs"] = new JsonArray(actor.DeepClone());
                File.WriteAllText(npcPath, npcRoot.ToJsonString());
            }
            if (addSecondNpc)
            {
                var npcPath = fileSystem.ResolvePath("game_state/npcs/npc_core.json");
                var npcRoot = JsonNode.Parse(File.ReadAllText(npcPath))!.AsObject();
                var actor = MortalActorTestFixtures.CreateActor(
                    "npc_publication_assistant_02",
                    "loc_field_clinic_001",
                    "Publication assistant");
                actor["activeSkills"] = new JsonArray();
                actor["passiveSkills"] = new JsonArray();
                Assert.IsType<JsonArray>(npcRoot["NPCsInScene"]).Add(actor);
                File.WriteAllText(npcPath, npcRoot.ToJsonString());
            }
            if (scenario.Publication)
                WriteUnrelatedGlobalEffect(fileSystem);
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/player/skill_mastery.json"),
                CreatePlayerSkillMastery(currentSource).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/inventory/items.json"),
                new JsonObject { ["items"] = new JsonArray() }.ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/inventory/item_identity_index.json"),
                MortalItemTestFixture.CreateIndexForCarriers().ToJsonString());
            Directory.CreateDirectory(Path.GetDirectoryName(
                fileSystem.ResolvePath("game_state/world/world_time.json"))!);
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/world/world_time.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["currentTimeInMinutes"] = 1_260
                }.ToJsonString());
            var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
                "loc_field_clinic_001",
                "Capability authority field clinic");
            File.WriteAllText(
                fileSystem.ResolvePath(MortalLocationMaterializationContract.WorldMapPath),
                MortalLocationTestFixture.CreateWorldMap(location).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath(MortalLocationMaterializationContract.CurrentLocationPath),
                MortalLocationTestFixture.CreateCurrentProjection(location).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath(MortalLocationIdentityState.StatePath),
                MortalLocationTestFixture.CreateIdentityIndex(location).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/meta/soul_state.json"),
                new JsonObject { ["currentRealm"] = "Mortal World" }.ToJsonString());
            var combatTarget = scenario.TargetKind is "combatant" or "combatant_member";
            var carrierPath = scenario.TargetKind == "combatant_member"
                ? WoundCarrierCatalog.AlliesPath
                : WoundCarrierCatalog.EnemiesPath;
            var woundRoot = combatTarget
                ? WoundContractTestData.CreateActiveWound(
                    woundId: WoundId,
                    ownerKind: scenario.TargetKind,
                    ownerId: scenario.TargetId,
                    carrierPath: carrierPath)
                : WoundContractTestData.CreateActiveWound(woundId: WoundId);
            ConfigureEffectlessMortalWound(woundRoot);
            if (scenario.Name ==
                "untouched_player_skill_preserves_unrelated_deterioration_anchor")
            {
                woundRoot["recovery"]!["deteriorationAnchor"]!["conditionKey"] =
                    "unsafe_environment";
            }
            if (scenario.Name == "fresh_untreated_empty_blockers_stabilize_exports")
            {
                woundRoot["recovery"]!["blockers"] = new JsonArray();
                woundRoot["recovery"]!["deteriorationAnchor"] = null;
            }
            woundRoot["treatment"]!["routes"] = new JsonArray(
                CreateGuaranteedRoute(scenario));
            woundRoot["treatment"]!["knownRouteIds"] = new JsonArray(RouteId);
            var parsed = WoundMaterializationContract.Parse(woundRoot.ToJsonString(), "wound");
            Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
            JsonObject? playerWoundsRoot = null;
            JsonObject? enemyCombatantsRoot = null;
            JsonObject? allyCombatantsRoot = null;
            if (combatTarget)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(
                    fileSystem.ResolvePath(carrierPath))!);
                var combatant = scenario.TargetKind == "combatant_member"
                    ? CreateProductionCombatGroup(
                        "combatant_group_01",
                        scenario.TargetId,
                        woundRoot)
                    : CreateProductionCombatant(scenario.TargetId, woundRoot);
                var combatantsRoot = new JsonObject
                {
                    [scenario.TargetKind == "combatant_member" ? "alliesData" : "enemiesData"] =
                        new JsonArray(combatant)
                };
                if (scenario.TargetKind == "combatant_member")
                    allyCombatantsRoot = combatantsRoot;
                else
                    enemyCombatantsRoot = combatantsRoot;
                File.WriteAllText(
                    fileSystem.ResolvePath(carrierPath),
                    combatantsRoot.ToJsonString());
            }
            else
            {
                playerWoundsRoot = WoundContractTestData.CreatePlayerCarrier(woundRoot);
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/player/wounds.json"),
                    playerWoundsRoot.ToJsonString());
            }
            var woundFingerprint = WoundIdentityState.ComputeSemanticFingerprint(parsed.Wound!);
            var identityRoot = WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry(
                    woundId: WoundId,
                    ownerKind: combatTarget ? scenario.TargetKind : "player",
                    ownerId: combatTarget ? scenario.TargetId : "player_current",
                    carrierPath: combatTarget ? carrierPath : "game_state/player/wounds.json",
                    semanticFingerprint: woundFingerprint));
            File.WriteAllText(
                fileSystem.ResolvePath(WoundIdentityState.StatePath),
                identityRoot.ToJsonString());
            var initialTransition = WoundContractTestData.CreateTransition();
            initialTransition["beforeFingerprint"] =
                WoundHistoryState.ComputeNonexistentBeforeFingerprint(WoundId);
            initialTransition["afterFingerprint"] = woundFingerprint;
            initialTransition["sourceFingerprint"] = "sha256:" + new string('e', 64);
            initialTransition["attemptId"] = null;
            var historyRoot = WoundContractTestData.CreateHistory(initialTransition);
            File.WriteAllText(
                fileSystem.ResolvePath(WoundHistoryState.HistoryPath),
                historyRoot.ToJsonString());
            var identity = WoundIdentityState.Parse(
                identityRoot.ToJsonString(),
                WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(
                historyRoot.ToJsonString(),
                WoundHistoryState.HistoryPath);
            var carriers = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
                PlayerWounds: playerWoundsRoot,
                NpcWounds: null,
                EnemyCombatants: enemyCombatantsRoot,
                AllyCombatants: allyCombatantsRoot,
                AfterlifeProfiles: null));
            Assert.True(identity.IsValid, DescribeIssues(identity.Issues));
            Assert.True(history.IsValid, DescribeIssues(history.Issues));
            Assert.Empty(carriers.Issues);
            Assert.Empty(history.State!.ValidateAgreement(identity.State!, carriers));
            WriteCanonicalResourceAuthority(fileSystem);
            var preparedTurn = new LiveTurnPreparationService(fileSystem).PrepareAsync(
                new LiveTurnPreparationOptions
                {
                    SessionId = "session_capability_authority",
                    RequestId = "request_capability_authority",
                    TurnNumber = 42,
                    PlayerAction = "Treat the active mortal wound with guaranteed field care.",
                    CurrentRealm = scenario.Name == "wrong_realm_rejects" ? "Chaos Sea" : "Mortal World",
                    PreGeneratedDices1d20 = new[] { 17 }
                }).GetAwaiter().GetResult();
            Assert.Equal(LiveTurnPreparationService.TurnRequestPath, preparedTurn.TurnRequestPath);
            Assert.Equal(LiveTurnPreparationService.PendingTurnSnapshotManifestPath, preparedTurn.ManifestPath);
            Assert.Equal(PendingTurnSnapshotAuthority.AuthorityPath, preparedTurn.AuthorityPath);
            Assert.Equal("session_capability_authority", preparedTurn.SessionId);
            Assert.Equal("request_capability_authority", preparedTurn.RequestId);
            Assert.Equal(42, preparedTurn.TurnNumber);
            Assert.True(preparedTurn.SnapshotFileCount > 0);
            Assert.True(File.Exists(fileSystem.ResolvePath(preparedTurn.ManifestPath)));
            Assert.True(File.Exists(fileSystem.ResolvePath(preparedTurn.AuthorityPath)));
            var lease = fileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
            return new CapabilityAuthorityFixture(
                root,
                fileSystem,
                lease,
                parsed.Wound!,
                preparedTurn,
                CreateSelectionContext(scenario, currentSource),
                scenario,
                currentSource,
                finalSource);
        }

        private static void MakeUnselectedSkillIdsOwnerUnique(
            JsonObject source,
            string selectedOwner)
        {
            var unselectedOwner = selectedOwner == "player" ? "npc" : "player";
            var owner = Assert.IsType<JsonObject>(source[unselectedOwner]);
            var prefix = unselectedOwner == "player" ? "player" : "npc";
            Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(owner["activeSkills"])))!["skillId"] =
                    $"skill_{prefix}_field_medicine_01";
            Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(owner["passiveSkills"])))!["skillId"] =
                    $"skill_{prefix}_triage_passive_01";
        }

        private static void ConfigureEffectlessMortalWound(JsonObject woundRoot)
        {
            Assert.Equal("untreated", woundRoot["care"]!["state"]!.GetValue<string>());
            Assert.Contains(
                "not_stabilized",
                woundRoot["recovery"]!["blockers"]!.AsArray()
                    .Select(static blocker => blocker!.GetValue<string>()));
            woundRoot["recovery"]!["blockers"] = new JsonArray(
                "not_stabilized",
                "unsafe_environment");
            woundRoot["recovery"]!["recoveryAnchor"] = new JsonObject
            {
                ["anchorKind"] = "creation",
                ["anchorMinute"] = 100,
                ["anchorTransitionId"] = "wound_transition_test_001"
            };
            woundRoot["recovery"]!["deteriorationAnchor"] = new JsonObject
            {
                ["conditionKey"] = "not_stabilized",
                ["anchorMinute"] = 100,
                ["anchorTransitionId"] = "wound_transition_test_001"
            };
            woundRoot["consequences"] = new JsonObject
            {
                ["slotBudget"] = 2,
                ["slotsUsed"] = 0,
                ["ownedEffectSources"] = WoundContractTestData.CreateOwnedEffectSources(
                    WoundId,
                    "mortal_world"),
                ["entries"] = new JsonArray()
            };
        }

        private static void WriteUnrelatedGlobalEffect(FileSystemManager fileSystem)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(
                fileSystem.ResolvePath(EffectCarrierCatalog.PlayerPath))!);
            Directory.CreateDirectory(Path.GetDirectoryName(
                fileSystem.ResolvePath(EffectIdentityState.StatePath))!);
            var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
                "player",
                "event_reaction");
            effect["effectId"] = UnrelatedEffectId;
            effect["display"] = new JsonObject
            {
                ["name"] = "Щит Судьбы",
                ["description"] =
                    "Чернила Судьбы смягчают следующий критический провал до обычного провала.",
                ["category"] = "buff",
                ["visibility"] = "visible"
            };
            effect["source"] = new JsonObject
            {
                ["kind"] = EffectBuiltInSourceCatalog.FateShieldSourceKind,
                ["sourceId"] = EffectBuiltInSourceCatalog.FateShieldSourceId,
                ["definitionKey"] = EffectBuiltInSourceCatalog.FateShieldDefinitionKey
            };
            effect["components"] = new JsonArray(new JsonObject
            {
                ["componentId"] = "fate_shield_reaction",
                ["profile"] = "event_reaction",
                ["priority"] = -100,
                ["payload"] = new JsonObject
                {
                    ["eventType"] = "owner_critical_failure",
                    ["resultKind"] = "event_outcome",
                    ["originalOutcome"] = "critical_failure",
                    ["resolvedOutcome"] = "failure",
                    ["dependency"] = "before_current_event",
                    ["maxExpansion"] = 1
                }
            });
            effect["lifetime"] = new JsonObject
            {
                ["mode"] = "uses",
                ["remainingUses"] = 1,
                ["consumingTriggerIds"] = new JsonArray(
                    "fate_shield_on_critical_failure"),
                ["displayText"] = "Until the next critical failure"
            };
            effect["stacking"] = new JsonObject
            {
                ["stackKey"] = "ink-feather-fate-shield",
                ["policy"] = "independent",
                ["maxStacks"] = 1,
                ["currentStacks"] = 1,
                ["refreshMode"] = null,
                ["mergeRule"] = null
            };
            effect["triggers"] = new JsonArray(new JsonObject
            {
                ["triggerId"] = "fate_shield_on_critical_failure",
                ["eventType"] = "owner_critical_failure",
                ["priority"] = -100,
                ["componentIds"] = new JsonArray("fate_shield_reaction"),
                ["consumeUses"] = true,
                ["resolutionMode"] = "deterministic"
            });
            effect["removal"] = new JsonObject
            {
                ["dispelCategories"] = new JsonArray("fate"),
                ["cureKinds"] = new JsonArray(),
                ["onSourceLoss"] = "no_change",
                ["onConditionLoss"] = null,
                ["manualAuthorities"] = new JsonArray()
            };
            effect["links"] = new JsonArray();

            File.WriteAllText(
                fileSystem.ResolvePath(EffectCarrierCatalog.PlayerPath),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["activeEffects"] = new JsonArray(effect.DeepClone())
                }.ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath(EffectIdentityState.StatePath),
                EffectMaterializationTestFixture.CreateIdentityIndex(effect)
                    .ToJsonString());
        }

        private static JsonObject CreatePlayerSkillMastery(JsonObject source)
        {
            var activeSkill = Skill(source, "player", "activeSkills");
            return new JsonObject
            {
                ["skillMasteryChanges"] = new JsonArray(new JsonObject
                {
                    ["skillName"] = activeSkill["skillName"]!.DeepClone(),
                    ["newMasteryLevel"] = 3,
                    ["newCurrentMasteryProgress"] = 0,
                    ["newMasteryProgressNeeded"] = 100,
                    ["masteryLeveledUp"] = false
                })
            };
        }

        private static JsonObject CreateProductionCombatant(
            string combatantId,
            JsonObject wound)
        {
            var row = CreateProductionCombatRow("Capability authority combatant");
            row["combatantId"] = combatantId;
            row["activeWounds"] = new JsonArray(wound.DeepClone());
            return row;
        }

        private static JsonObject CreateProductionCombatGroup(
            string combatantId,
            string memberId,
            JsonObject wound)
        {
            var row = CreateProductionCombatRow("Capability authority combat group");
            row["combatantId"] = combatantId;
            row["isGroup"] = true;
            row["count"] = 1;
            row["unitName"] = "member";
            row["members"] = new JsonArray(new JsonObject
            {
                ["memberId"] = memberId,
                ["displayName"] = "Capability authority combat member",
                ["activeWounds"] = new JsonArray(wound.DeepClone())
            });
            return row;
        }

        private static JsonObject CreateProductionCombatRow(string name) => new()
        {
            ["NPCId"] = null,
            ["name"] = name,
            ["image_prompt"] = "setting neutral combatant portrait",
            ["description"] = "A complete canonical combat row used by capability authority tests.",
            ["type"] = "Test combatant",
            ["isGroup"] = false,
            ["actions"] = new JsonArray(),
            ["resistances"] = new JsonArray(),
            ["activeBuffs"] = new JsonArray(),
            ["activeDebuffs"] = new JsonArray()
        };

        private static void WriteCanonicalResourceAuthority(FileSystemManager fileSystem)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fileSystem.ResolvePath(
                ResourceMaterializationContract.DefinitionsPath))!);
            var bootstrap = ResourceBootstrapStateBuilder.BuildMortalPlayer(
                incarnationNumber: 1,
                turn: 1,
                permanentStrength: 10,
                permanentConstitution: 10,
                permanentIntelligence: 10,
                permanentWisdom: 10,
                permanentFaith: 10);
            Assert.True(bootstrap.IsValid, DescribeIssues(bootstrap.Issues));
            var definitions = Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions);
            var state = Assert.IsType<ResourceStateLedger>(bootstrap.State);
            var history = Assert.IsType<ResourceHistoryState>(bootstrap.History);
            File.WriteAllText(
                fileSystem.ResolvePath(ResourceMaterializationContract.DefinitionsPath),
                definitions.ToCanonicalJson());
            File.WriteAllText(
                fileSystem.ResolvePath(ResourceMaterializationContract.StatePath),
                state.ToCanonicalJson());
            File.WriteAllText(
                fileSystem.ResolvePath(ResourceMaterializationContract.HistoryPath),
                history.ToCanonicalJson());

            var composed = CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                    definitions,
                    path => Task.FromResult<string?>(File.Exists(fileSystem.ResolvePath(path))
                        ? File.ReadAllText(fileSystem.ResolvePath(path))
                        : null),
                    state,
                    history,
                    CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap)
                .GetAwaiter()
                .GetResult();
            Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
            Assert.False(string.IsNullOrWhiteSpace(composed.CanonicalAuthorityJson));
            File.WriteAllText(
                fileSystem.ResolvePath(CanonicalResourceOwnerAuthorityComposer.AuthorityPath),
                composed.CanonicalAuthorityJson);
        }

        public void Dispose()
        {
            if (_lease is not null)
            {
                _lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _lease = null;
            }
            Directory.Delete(Root, recursive: true);
        }

        internal void ReleaseLeaseForTopLevelPublication()
        {
            var lease = Lease;
            _lease = null;
            lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        internal void ReacquireLeaseAfterTopLevelPublication()
        {
            Assert.Null(_lease);
            _lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
        }

        private static JsonObject CreateSelectionContext(
            CapabilityScenario scenario,
            JsonObject currentSource) => new()
        {
            ["schemaVersion"] = 1,
            ["realm"] = currentSource["realm"]!.GetValue<string>(),
            ["targetKind"] = scenario.TargetKind,
            ["targetId"] = scenario.TargetId,
            ["providerKind"] = scenario.ProviderKind,
            ["providerId"] = scenario.ProviderId,
            ["currentLocationId"] = "loc_field_clinic_001"
        };

        internal void MutatePersistedSelectedSourceAfterExport()
        {
            var skill = Skill(CurrentSource, Scenario.SourceOwner, Scenario.SourceSkillArray);
            skill["displayName"] = "Mutated only after proof export";
            WriteCanonicalSkillSources(FileSystem, CurrentSource);
        }

        internal void MutatePersistedSelectedCapabilityAfterExport()
        {
            var skill = Skill(CurrentSource, Scenario.SourceOwner, Scenario.SourceSkillArray);
            Limits(Capability(skill))["maximumRecoveryPoints"] = 1;
            WriteCanonicalSkillSources(FileSystem, CurrentSource);
        }

        internal WoundHistoryParseResult ReadCurrentHistory()
        {
            var parsed = WoundHistoryState.Parse(
                File.ReadAllText(FileSystem.ResolvePath(WoundHistoryState.HistoryPath)),
                WoundHistoryState.HistoryPath);
            Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
            Assert.NotNull(parsed.State);
            return parsed;
        }

        internal GameResponse CreatePublicationProposal()
        {
            // This is the only publication input owned by the test: an ordinary GM
            // response assembled from scenario operations. FinalSource is an oracle
            // only: neither it nor a serialised canonical root enters the proposal.
            var proposal = new GameResponse();
            if (!UsesFinalAfterImage)
                return proposal;
            var proposalRows = CreateScenarioProposalRows();
            AddPlayerProposalChanges(proposal, proposalRows, "activeSkills");
            AddPlayerProposalChanges(proposal, proposalRows, "passiveSkills");
            AddNpcProposalChanges(proposal, proposalRows, "activeSkills");
            AddNpcProposalChanges(proposal, proposalRows, "passiveSkills");
            return proposal;
        }

        internal GameResponse CreateCombinedNpcSkillPublicationProposal()
        {
            var active = Skill(CurrentSource, "npc", "activeSkills")
                .DeepClone().AsObject();
            var passive = Skill(CurrentSource, "npc", "passiveSkills")
                .DeepClone().AsObject();
            active["displayName"] = "Combined active publication wording";
            passive["displayName"] = "Combined passive publication wording";
            return new GameResponse
            {
                NPCActiveSkillChanges = new[]
                {
                    JsonSerializer.SerializeToElement(new JsonObject
                    {
                        ["npcId"] = Scenario.ProviderId,
                        ["skillChanges"] = new JsonArray(active)
                    })
                },
                NPCPassiveSkillChanges = new[]
                {
                    JsonSerializer.SerializeToElement(new JsonObject
                    {
                        ["npcId"] = Scenario.ProviderId,
                        ["skillChanges"] = new JsonArray(passive)
                    })
                }
            };
        }

        internal GameResponse CreateMultipleNpcSkillPublicationProposal()
        {
            var proposal = CreatePublicationProposal();
            var selected = Assert.Single(proposal.NPCActiveSkillChanges!);
            proposal.NPCActiveSkillChanges = new[]
            {
                selected,
                JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["npcId"] = "npc_publication_assistant_02",
                    ["skillChanges"] = new JsonArray()
                })
            };
            return proposal;
        }

        internal GameResponse CreateSynthesizingNpcSkillPublicationProposal()
        {
            var proposal = CreatePublicationProposal();
            var selected = Assert.Single(proposal.NPCActiveSkillChanges!);
            var synthesized = Skill(CurrentSource, "npc", "activeSkills")
                .DeepClone().AsObject();
            synthesized["skillId"] = "skill_publication_assistant_care_02";
            synthesized["skillName"] = "Synthesized assistant care";
            synthesized["displayName"] = "Synthesized assistant care";
            proposal.NPCActiveSkillChanges = new[]
            {
                selected,
                JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["npcId"] = "npc_publication_assistant_02",
                    ["skillChanges"] = new JsonArray(synthesized)
                })
            };
            return proposal;
        }

        internal JsonObject CreateSynthesizedAssistantActiveRoot()
        {
            var synthesized = Skill(CurrentSource, "npc", "activeSkills")
                .DeepClone().AsObject();
            synthesized["skillId"] = "skill_publication_assistant_care_02";
            synthesized["skillName"] = "Synthesized assistant care";
            synthesized["displayName"] = "Synthesized assistant care";
            return new JsonObject
            {
                ["activeSkills"] = new JsonArray(synthesized)
            };
        }

        internal void AssertCombinedNpcSkillProjection(object value)
        {
            var plan = Assert.IsType<AcceptedMechanicsPlan>(value);
            var after = Assert.Single(plan.OwnerCompanionAfterImages);
            Assert.Equal("game_state/npcs/npc_core.json", after.Key);
            var actor = Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(after.Value["NPCsInScene"])));
            Assert.Equal(
                "Combined active publication wording",
                Assert.IsType<JsonObject>(Assert.Single(
                    Assert.IsType<JsonArray>(actor["activeSkills"])))
                    ["displayName"]!.GetValue<string>());
            Assert.Equal(
                "Combined passive publication wording",
                Assert.IsType<JsonObject>(Assert.Single(
                    Assert.IsType<JsonArray>(actor["passiveSkills"])))
                    ["displayName"]!.GetValue<string>());
        }

        internal void AssertMirroredNpcProjection(object value)
        {
            var plan = Assert.IsType<AcceptedMechanicsPlan>(value);
            var npcRoot = plan.OwnerCompanionAfterImages["game_state/npcs/npc_core.json"];
            var updated = Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(npcRoot["UpdateNPCs"])));
            var inScene = Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(npcRoot["NPCsInScene"])));
            Assert.True(JsonNode.DeepEquals(updated, inScene));
            var skill = Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(updated["activeSkills"])));
            Assert.Equal(
                "Publication-only diagnostic wording",
                skill["displayName"]!.GetValue<string>());
        }

        internal void AssertMultipleNpcSkillProjection(object value)
        {
            var plan = Assert.IsType<AcceptedMechanicsPlan>(value);
            var after = Assert.Single(plan.OwnerCompanionAfterImages);
            Assert.Equal("game_state/npcs/npc_core.json", after.Key);
            var actors = Assert.IsType<JsonArray>(after.Value["NPCsInScene"])
                .OfType<JsonObject>()
                .ToArray();
            Assert.Equal(2, actors.Length);
            var assistant = Assert.Single(actors, static actor =>
                actor["NPCId"]?.GetValue<string>() == "npc_publication_assistant_02");
            Assert.Empty(Assert.IsType<JsonArray>(assistant["activeSkills"]));
            Assert.Empty(Assert.IsType<JsonArray>(assistant["passiveSkills"]));
        }

        internal void MutateNpcMirror(string mutation)
        {
            var path = FileSystem.ResolvePath("game_state/npcs/npc_core.json");
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var inScene = Assert.IsType<JsonArray>(root["NPCsInScene"]);
            var actor = Assert.IsType<JsonObject>(Assert.Single(inScene));
            switch (mutation)
            {
                case "divergent_cross_section":
                    var mirrored = Assert.IsType<JsonObject>(Assert.Single(
                        Assert.IsType<JsonArray>(root["UpdateNPCs"])));
                    mirrored["displayName"] = "Divergent publication mirror";
                    break;
                case "same_section_duplicate":
                    inScene.Add(actor.DeepClone());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }
            File.WriteAllText(path, root.ToJsonString());
        }

        internal void AddConfusableNpcSiblingAfterPublicationComposition()
        {
            var path = FileSystem.ResolvePath("game_state/npcs/npc_core.json");
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var actors = Assert.IsType<JsonArray>(root["NPCsInScene"]);
            var selected = Assert.IsType<JsonObject>(Assert.Single(actors));
            var confusable = selected.DeepClone().AsObject();
            confusable["NPCId"] = Scenario.ProviderId.Replace(
                "i",
                "і",
                StringComparison.Ordinal);
            actors.Add(confusable);
            File.WriteAllText(path, root.ToJsonString());
        }

        private JsonObject CreateScenarioProposalRows()
        {
            // This is deliberately independent from FinalSource: it models only
            // legal GM commands against the live before rows, while FinalSource is
            // the separately derived assertion oracle for T070 normalization.
            var rows = CurrentSource.DeepClone().AsObject();
            var selected = Skill(rows, Scenario.SourceOwner, Scenario.SourceSkillArray);
            var capability = Capability(selected);
            switch (Scenario.Name)
            {
                case "removed_final_skill_rejects":
                    rows[Scenario.SourceOwner]![Scenario.SourceSkillArray] = new JsonArray();
                    break;
                case "retired_final_skill_rejects":
                    selected["lifecycle"] = "retired";
                    break;
                case "changed_final_capability_ref_rejects":
                    capability["capabilityRef"] = "after_image_changed_capability";
                    break;
                case "changed_final_domain_rejects":
                    capability["woundDomain"] = "spiritual";
                    break;
                case "changed_final_operation_limits_reject":
                case "changed_final_player_passive_operation_limits_reject":
                case "changed_final_npc_passive_operation_limits_reject":
                    Limits(capability)["maximumRecoveryPoints"] = 2;
                    break;
                case "stale_final_source_rejects":
                    selected["active"] = false;
                    break;
                case "duplicate_or_confusable_final_skill_row_rejects":
                    OtherSkill(rows, Scenario)["skillId"] = selected["skillId"]!.DeepClone();
                    OtherSkill(rows, Scenario)["skillName"] =
                        "Publication sibling replacement";
                    break;
                case "confusable_final_skill_sibling_rejects":
                    OtherSkill(rows, Scenario)["skillId"] =
                        Scenario.SkillId.Replace("i", "і", StringComparison.Ordinal);
                    OtherSkill(rows, Scenario)["skillName"] =
                        "Publication sibling replacement";
                    break;
                case "changed_final_skill_id_rejects":
                    // Existing composition selects an update by exact skillId/name;
                    // replacement identity must therefore be an explicit remove/add,
                    // not a fictional in-place ID rewrite.
                    selected["skillId"] = "skill_after_image_changed_01";
                    selected["skillName"] = "After-image Field Medicine";
                    break;
                case "touched_player_active_skill_reads_exact_final_after_image":
                case "touched_player_passive_skill_reads_exact_final_after_image":
                case "touched_npc_active_skill_reads_exact_final_after_image":
                case "touched_npc_passive_skill_reads_exact_final_after_image":
                    selected["displayName"] = "Publication-only diagnostic wording";
                    break;
            }
            return rows;
        }

        internal void AssertPublicationProposalIsNotFinalPlanAuthority(GameResponse proposal)
        {
            Assert.NotNull(proposal);
            Assert.DoesNotContain(
                proposal.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Select(static property => property.Name),
                static name => name.Contains("Plan", StringComparison.Ordinal) ||
                               name.Contains("AfterImage", StringComparison.Ordinal) ||
                               name.Contains("Fingerprint", StringComparison.Ordinal) ||
                               name.Contains("Authority", StringComparison.Ordinal) ||
                               name.Contains("Proof", StringComparison.Ordinal));
            Assert.Null(proposal.WoundDecisions);

            if (!UsesFinalAfterImage)
            {
                Assert.Null(proposal.ActiveSkillChanges);
                Assert.Null(proposal.RemoveActiveSkills);
                Assert.Null(proposal.PassiveSkillChanges);
                Assert.Null(proposal.RemovePassiveSkills);
                Assert.Null(proposal.NPCActiveSkillChanges);
                Assert.Null(proposal.NPCPassiveSkillChanges);
                return;
            }

            // A touched proposal carries a complete selected skill row (or a
            // genuine removal command) and never a serialised final canonical root.
            var proposalJson = JsonSerializer.Serialize(proposal);
            Assert.DoesNotContain("OwnerCompanionAfterImages", proposalJson, StringComparison.Ordinal);
            Assert.DoesNotContain("AcceptedMechanicsPlan", proposalJson, StringComparison.Ordinal);
            Assert.DoesNotContain("ProofFingerprint", proposalJson, StringComparison.Ordinal);
            Assert.DoesNotContain("SourceSemanticFingerprint", proposalJson, StringComparison.Ordinal);
            Assert.True(
                proposal.ActiveSkillChanges is { Length: > 0 } ||
                proposal.RemoveActiveSkills is { Length: > 0 } ||
                proposal.PassiveSkillChanges is { Length: > 0 } ||
                proposal.RemovePassiveSkills is { Length: > 0 } ||
                proposal.NPCActiveSkillChanges is { Length: > 0 } ||
                proposal.NPCPassiveSkillChanges is { Length: > 0 });
            var proposalRows = CreateScenarioProposalRows();
            AssertProposalCarriesExactSelectedSkillChange(proposal, proposalRows);
            AssertProposalCarriesEveryExpectedPlayerRootChange(proposal, proposalRows);
        }

        private void AssertProposalCarriesEveryExpectedPlayerRootChange(
            GameResponse proposal,
            JsonObject proposalRows)
        {
            foreach (var path in ExpectedPublicationRoots(proposalRows).Keys
                         .Where(static path => path.StartsWith("game_state/player/", StringComparison.Ordinal)))
            {
                var skillArray = path.EndsWith("skills_active.json", StringComparison.Ordinal)
                    ? "activeSkills"
                    : "passiveSkills";
                if (path == SelectedSkillRootPath)
                    continue;

                var expected = Assert.IsType<JsonArray>(proposalRows["player"]![skillArray]);
                var actual = skillArray == "activeSkills"
                    ? proposal.ActiveSkillChanges
                    : proposal.PassiveSkillChanges;
                Assert.NotNull(actual);
                Assert.Equal(expected.ToJsonString(), new JsonArray(actual!
                    .Select(static row => JsonNode.Parse(row.GetRawText())).ToArray()).ToJsonString());
            }
        }

        private void AssertProposalCarriesExactSelectedSkillChange(
            GameResponse proposal,
            JsonObject proposalRows)
        {
            var finalRows = Assert.IsType<JsonArray>(
                proposalRows[Scenario.SourceOwner]![Scenario.SourceSkillArray]);
            if (Scenario.SourceOwner == "player")
            {
                var changes = Scenario.SourceSkillArray == "activeSkills"
                    ? proposal.ActiveSkillChanges
                    : proposal.PassiveSkillChanges;
                var removals = Scenario.SourceSkillArray == "activeSkills"
                    ? proposal.RemoveActiveSkills
                    : proposal.RemovePassiveSkills;
                if (finalRows.Count == 0)
                {
                    Assert.Null(changes);
                    Assert.Equal(new[] { SelectedSkillName }, removals);
                    return;
                }

                if (Scenario.Name == "changed_final_skill_id_rejects")
                    Assert.Equal(new[] { SelectedSkillName }, removals);
                else
                    Assert.Null(removals);
                var actual = Assert.Single(changes!);
                Assert.Equal(finalRows[0]!.ToJsonString(), actual.GetRawText());
                return;
            }

            var npcChanges = Scenario.SourceSkillArray == "activeSkills"
                ? proposal.NPCActiveSkillChanges
                : proposal.NPCPassiveSkillChanges;
            var npcChange = JsonNode.Parse(Assert.Single(npcChanges!).GetRawText())!.AsObject();
            Assert.Equal(Scenario.ProviderId, npcChange["npcId"]!.GetValue<string>());
            if (finalRows.Count == 0)
            {
                Assert.Equal(new[] { SelectedSkillName }, npcChange["skillsToRemove"]!.AsArray()
                    .Select(static value => value!.GetValue<string>()));
                Assert.Null(npcChange["skillChanges"]);
                return;
            }

            if (Scenario.Name == "changed_final_skill_id_rejects")
                Assert.Equal(new[] { SelectedSkillName }, npcChange["skillsToRemove"]!.AsArray()
                    .Select(static value => value!.GetValue<string>()));
            else
                Assert.Null(npcChange["skillsToRemove"]);
            var actualRows = npcChange["skillChanges"]!.AsArray();
            Assert.Equal(finalRows.ToJsonString(), actualRows.ToJsonString());
        }

        private void AddPlayerProposalChanges(
            GameResponse proposal,
            JsonObject proposalRows,
            string skillArray)
        {
            var current = Assert.IsType<JsonArray>(CurrentSource["player"]![skillArray]);
            var final = Assert.IsType<JsonArray>(proposalRows["player"]![skillArray]);
            var selectedArray = string.Equals(skillArray, Scenario.SourceSkillArray, StringComparison.Ordinal) &&
                                string.Equals(Scenario.SourceOwner, "player", StringComparison.Ordinal);
            if (JsonNode.DeepEquals(current, final) && !selectedArray)
                return;
            if (selectedArray && final.Count == 0)
            {
                if (skillArray == "activeSkills")
                    proposal.RemoveActiveSkills = new[] { SelectedSkillName };
                else
                    proposal.RemovePassiveSkills = new[] { SelectedSkillName };
                return;
            }

            var changes = ToProposalElements(final);
            if (skillArray == "activeSkills")
                proposal.ActiveSkillChanges = changes;
            else
                proposal.PassiveSkillChanges = changes;
            if (!selectedArray && Scenario.Name is
                    "duplicate_or_confusable_final_skill_row_rejects" or
                    "confusable_final_skill_sibling_rejects")
            {
                var original = Assert.IsType<JsonObject>(Assert.Single(current));
                var originalName = original["skillName"]!.GetValue<string>();
                if (skillArray == "activeSkills")
                    proposal.RemoveActiveSkills = new[] { originalName };
                else
                    proposal.RemovePassiveSkills = new[] { originalName };
            }
            if (selectedArray && Scenario.Name == "changed_final_skill_id_rejects")
            {
                if (skillArray == "activeSkills")
                    proposal.RemoveActiveSkills = new[] { SelectedSkillName };
                else
                    proposal.RemovePassiveSkills = new[] { SelectedSkillName };
            }
        }

        private void AddNpcProposalChanges(
            GameResponse proposal,
            JsonObject proposalRows,
            string skillArray)
        {
            var current = Assert.IsType<JsonArray>(CurrentSource["npc"]![skillArray]);
            var final = Assert.IsType<JsonArray>(proposalRows["npc"]![skillArray]);
            var selectedArray = string.Equals(skillArray, Scenario.SourceSkillArray, StringComparison.Ordinal) &&
                                string.Equals(Scenario.SourceOwner, "npc", StringComparison.Ordinal);
            if (JsonNode.DeepEquals(current, final) && !selectedArray)
                return;
            var npcChange = new JsonObject
            {
                ["npcId"] = Scenario.ProviderId
            };
            if (selectedArray && final.Count == 0)
                npcChange["skillsToRemove"] = new JsonArray(SelectedSkillName);
            else
                npcChange["skillChanges"] = final.DeepClone();
            if (selectedArray && Scenario.Name == "changed_final_skill_id_rejects")
                npcChange["skillsToRemove"] = new JsonArray(SelectedSkillName);
            var changes = new[] { JsonSerializer.SerializeToElement(npcChange) };
            if (skillArray == "activeSkills")
                proposal.NPCActiveSkillChanges = changes;
            else
                proposal.NPCPassiveSkillChanges = changes;
        }

        private static JsonElement[] ToProposalElements(JsonArray rows) =>
            rows.Select(static row => JsonSerializer.SerializeToElement(row)).ToArray();

        internal void ApplyLiveExporterMutation()
        {
            if (Scenario.ExpectedBoundary != CapabilityFailureBoundary.Exporter || Scenario.Publication)
                return;

            var source = Skill(CurrentSource, Scenario.SourceOwner, Scenario.SourceSkillArray);
            switch (Scenario.Name)
            {
                case "inactive_skill_rejects":
                    source["active"] = false;
                    break;
                case "retired_skill_rejects":
                    source["lifecycle"] = "retired";
                    break;
                case "wrong_owner_rejects":
                    Assert.Equal("npc", Scenario.SourceOwner);
                    Assert.IsType<JsonObject>(CurrentSource["npc"])["ownerId"] = "npc_wrong_owner_01";
                    break;
                // Wrong role and an unpromoted combat target retain the sealed valid
                // context/root and are rejected solely by the capability exporter.
                case "wrong_role_rejects":
                case "target_owned_combatant_requires_promotion":
                    break;
                default:
                    throw new InvalidOperationException(
                        $"No live exporter mutation is defined for {Scenario.Name}.");
            }

            WriteCanonicalSkillSources(FileSystem, CurrentSource);
        }

        internal void AssertAcceptedCombatWoundCarrier()
        {
            Assert.True(Scenario.TargetKind is "combatant" or "combatant_member");
            var enemy = ReadRoot(WoundCarrierCatalog.EnemiesPath);
            var ally = ReadRoot(WoundCarrierCatalog.AlliesPath);
            var catalog = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
                PlayerWounds: null,
                NpcWounds: null,
                EnemyCombatants: enemy,
                AllyCombatants: ally,
                AfterlifeProfiles: null));
            Assert.Empty(catalog.Issues);
            Assert.True(catalog.TryResolveOne(WoundId, out var occurrence));
            Assert.Equal(Scenario.TargetKind, occurrence.Coordinate.OwnerKind);
            Assert.Equal(Scenario.TargetId, occurrence.Coordinate.OwnerId);
            Assert.Equal(
                Scenario.TargetKind == "combatant_member"
                    ? WoundCarrierCatalog.AlliesPath
                    : WoundCarrierCatalog.EnemiesPath,
                occurrence.Coordinate.CarrierPath);
        }

        internal void AssertPristineEffectMechanicsSnapshot()
        {
            var snapshot = EffectMechanicsSnapshot.LoadAsync(FileSystem, Lease)
                .GetAwaiter().GetResult();
            Assert.True(snapshot.IsAccepted, DescribeIssues(snapshot.Issues));
            Assert.Empty(snapshot.Issues);
            Assert.Empty(snapshot.Components);
            Assert.Empty(snapshot.Effects);
        }

        internal void AssertUnrelatedEffectMechanicsSnapshot()
        {
            var snapshot = EffectMechanicsSnapshot.LoadAsync(FileSystem, Lease)
                .GetAwaiter().GetResult();
            Assert.True(snapshot.IsAccepted, DescribeIssues(snapshot.Issues));
            Assert.Empty(snapshot.Issues);
            Assert.Contains(
                snapshot.Effects,
                static effect => string.Equals(
                    effect.EffectId,
                    UnrelatedEffectId,
                    StringComparison.Ordinal));
            Assert.Contains(
                snapshot.Components,
                static component => string.Equals(
                    component.EffectId,
                    UnrelatedEffectId,
                    StringComparison.Ordinal));
        }

        internal void AssertProductionValidSkillShapes()
        {
            var player = Assert.IsType<JsonObject>(CurrentSource["player"]);
            var npc = Assert.IsType<JsonObject>(CurrentSource["npc"]);
            var playerActiveSkill = Assert.IsType<JsonObject>(
                Assert.Single(Assert.IsType<JsonArray>(player["activeSkills"])));
            var npcActiveSkill = Assert.IsType<JsonObject>(
                Assert.Single(Assert.IsType<JsonArray>(npc["activeSkills"])));
            Assert.False(playerActiveSkill.ContainsKey("currentMasteryLevel"));
            Assert.Equal(3, npcActiveSkill["currentMasteryLevel"]!.GetValue<int>());
            Assert.True(
                ValidationService.IsProductionValidMortalActiveSkill(ParseFirstSkill(player, "activeSkills")));
            Assert.True(
                ValidationService.IsProductionValidMortalPassiveSkill(ParseFirstSkill(player, "passiveSkills")));
            Assert.True(
                ValidationService.IsProductionValidMortalActiveSkill(ParseFirstSkill(npc, "activeSkills")));
            Assert.True(
                ValidationService.IsProductionValidMortalPassiveSkill(ParseFirstSkill(npc, "passiveSkills")));

            var npcRoot = ReadRoot("game_state/npcs/npc_core.json");
            var persistedNpc = Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(npcRoot!["NPCsInScene"])));
            Assert.Equal("npc_field_medic_01", persistedNpc["NPCId"]!.GetValue<string>());
            Assert.True(ValidationService.IsProductionValidMortalActiveSkill(
                ParseFirstSkill(persistedNpc, "activeSkills")));
            Assert.True(ValidationService.IsProductionValidMortalPassiveSkill(
                ParseFirstSkill(persistedNpc, "passiveSkills")));

            var publicationNpcRoot = PublicationRoot(CurrentSource, "game_state/npcs/npc_core.json");
            Assert.False(publicationNpcRoot.ContainsKey("NPCs"));
            var publicationNpc = Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(publicationNpcRoot["NPCsInScene"])));
            Assert.True(JsonNode.DeepEquals(persistedNpc, publicationNpc));
        }

        internal void AssertPreparedLiveTurnMatchesExportedBinding(object binding)
        {
            var acceptedBinding = Assert.IsType<WoundAcceptedTurnBinding>(binding);
            var turnRequest = ReadRoot(LiveTurnPreparationService.TurnRequestPath);
            Assert.NotNull(turnRequest);
            Assert.Equal(PreparedTurn.SessionId, turnRequest!["sessionId"]!.GetValue<string>());
            Assert.Equal(PreparedTurn.RequestId, turnRequest["requestId"]!.GetValue<string>());
            Assert.Equal(PreparedTurn.TurnNumber, turnRequest["turnNumber"]!.GetValue<int>());
            Assert.Equal("normal", turnRequest["gameMode"]!.GetValue<string>());
            Assert.Equal(new[] { 17 }, turnRequest["preGeneratedDices1d20"]!.AsArray()
                .Select(static die => die!.GetValue<int>()));
            Assert.Equal(PreparedTurn.SessionId, acceptedBinding.SessionId);
            Assert.Equal(PreparedTurn.RequestId, acceptedBinding.RequestId);
            Assert.Equal(PreparedTurn.TurnNumber, acceptedBinding.Turn);
            Assert.Equal("mortal_world", acceptedBinding.Realm);
            var manifest = ReadRoot(PreparedTurn.ManifestPath);
            Assert.NotNull(manifest);
            Assert.Equal(PreparedTurn.SessionId, manifest!["sessionId"]!.GetValue<string>());
            Assert.Equal(PreparedTurn.RequestId, manifest["requestId"]!.GetValue<string>());
            Assert.Equal(PreparedTurn.TurnNumber, manifest["turnNumber"]!.GetValue<int>());
            var manifestPayloadHash = manifest["manifestPayloadHash"]!.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(manifestPayloadHash));
            Assert.Equal(manifestPayloadHash, acceptedBinding.SnapshotToken);
            Assert.NotEmpty(acceptedBinding.AcceptedEvents);
            Assert.False(string.IsNullOrWhiteSpace(acceptedBinding.AcceptedEventsFingerprint));
            var acceptedEvent = Assert.Single(
                acceptedBinding.AcceptedEvents,
                static value => value.EventRef == EventRef);
            Assert.Equal(EventKind, acceptedEvent.Kind);
            Assert.Equal(EventAuthorityId, acceptedEvent.AuthorityId);
            Assert.Equal($"turn_{acceptedBinding.Turn}:accepted_effect", acceptedEvent.EventRef);
            Assert.Equal($"turn_{acceptedBinding.Turn}", acceptedEvent.AuthorityId);
            Assert.False(string.IsNullOrWhiteSpace(acceptedEvent.SemanticFingerprint));
        }

        internal string SelectedPublicationRootPath => SelectedSkillRootPath;

        internal IReadOnlyDictionary<string, PublicationBeforeImage>
            CaptureGovernedPublicationBeforeImages()
        {
            Assert.NotEmpty(GovernedPublicationPaths);
            Assert.Contains(SelectedSkillRootPath, GovernedPublicationPaths);
            Assert.Equal(
                GovernedPublicationPaths,
                CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static path => path, StringComparer.Ordinal));

            return GovernedPublicationPaths.ToDictionary(
                static path => path,
                path =>
                {
                    var physicalPath = FileSystem.ResolvePath(path);
                    var existed = File.Exists(physicalPath);
                    return new PublicationBeforeImage(
                        existed,
                        existed ? File.ReadAllBytes(physicalPath) : null);
                },
                StringComparer.Ordinal);
        }

        internal void AssertPublicationPlanUsesOnlyGovernedPaths(
            object plan,
            IReadOnlyDictionary<string, PublicationBeforeImage> governedBeforeImages)
        {
            var acceptedPlan = Assert.IsType<AcceptedMechanicsPlan>(plan);
            var planPaths = acceptedPlan.TouchedPaths
                .Concat(acceptedPlan.OwnerCompanionAfterImages.Keys)
                .Concat(acceptedPlan.EffectCarrierAfterImages.Keys)
                .Concat(acceptedPlan.PendingAfterImages.Keys)
                .Concat(acceptedPlan.ConsumedPaths)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Assert.NotEmpty(planPaths);
            Assert.All(planPaths, path => Assert.Contains(path, governedBeforeImages.Keys));
            Assert.All(
                planPaths,
                path => Assert.True(
                    acceptedPlan.BeforeImages.ContainsKey(path),
                    $"Publication mutation '{path}' has no exact before-image."));
        }

        internal void AssertPublicationPublishedExactSkillRoots(
            object plan,
            IReadOnlyDictionary<string, PublicationBeforeImage> beforeImages)
        {
            var afterImages = Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonObject>>(
                ReadRequiredProperty(plan, "OwnerCompanionAfterImages"));
            var expectedRoots = UsesFinalAfterImage
                ? ExpectedCommonPlanPublicationRoots(FinalSource)
                : new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            Assert.Equal(expectedRoots.Keys.OrderBy(static path => path),
                afterImages.Keys.OrderBy(static path => path));

            if (!UsesFinalAfterImage)
            {
                Assert.Empty(afterImages);
                Assert.True(beforeImages.TryGetValue(SelectedSkillRootPath, out var before));
                AssertCanonicalBytesEqual(SelectedSkillRootPath, before!);
                return;
            }

            foreach (var (path, expected) in expectedRoots)
            {
                Assert.True(beforeImages.ContainsKey(path),
                    $"Publication omitted governed before-image '{path}'.");
                Assert.True(afterImages.TryGetValue(path, out var planAfter));
                Assert.True(JsonNode.DeepEquals(expected, planAfter),
                    $"Plan after-image at '{path}' differs from the independent final-root oracle.");
                var actual = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(path)));
                Assert.True(JsonNode.DeepEquals(expected, actual),
                    $"Normalizer did not publish the exact after-image at '{path}'.");
            }
        }

        internal void AssertUnrelatedGlobalEffectPreserved(
            IReadOnlyDictionary<string, PublicationBeforeImage> beforeImages)
        {
            foreach (var path in new[]
                     {
                         EffectCarrierCatalog.PlayerPath,
                         EffectIdentityState.StatePath
                     })
            {
                Assert.True(beforeImages.TryGetValue(path, out var before));
                Assert.True(before!.Existed);
                Assert.NotNull(before.Bytes);
                var expected = JsonNode.Parse(before.Bytes!);
                var actual = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(path)));
                Assert.True(
                    JsonNode.DeepEquals(expected, actual),
                    $"Unrelated global effect authority changed at '{path}'.");
            }

            var carrier = ReadRoot(EffectCarrierCatalog.PlayerPath);
            Assert.Contains(
                carrier!["activeEffects"]!.AsArray().OfType<JsonObject>(),
                static effect => string.Equals(
                    effect["effectId"]!.GetValue<string>(),
                    UnrelatedEffectId,
                    StringComparison.Ordinal));
            var identity = ReadRoot(EffectIdentityState.StatePath);
            Assert.Contains(
                identity!["entries"]!.AsArray().OfType<JsonObject>(),
                static entry => string.Equals(
                    entry["effectId"]!.GetValue<string>(),
                    UnrelatedEffectId,
                    StringComparison.Ordinal));
        }

        internal void MutateGovernedRootAfterComposition()
        {
            var root = JsonNode.Parse(File.ReadAllText(
                FileSystem.ResolvePath(SelectedSkillRootPath)))!.AsObject();
            root["_stalePublicationProbe"] = "mutated-after-t070-compose";
            File.WriteAllText(FileSystem.ResolvePath(SelectedSkillRootPath), root.ToJsonString());
        }

        internal void AssertGovernedRootsMatchBeforeImages(
            IReadOnlyDictionary<string, PublicationBeforeImage> beforeImages)
        {
            Assert.Equal(
                GovernedPublicationPaths,
                beforeImages.Keys.OrderBy(static path => path, StringComparer.Ordinal));
            foreach (var (path, before) in beforeImages)
                AssertCanonicalBytesEqual(path, before);
        }

        private void AssertCanonicalBytesEqual(string path, PublicationBeforeImage before)
        {
            var physicalPath = FileSystem.ResolvePath(path);
            Assert.Equal(before.Existed, File.Exists(physicalPath));
            if (before.Existed)
            {
                Assert.NotNull(before.Bytes);
                Assert.True(before.Bytes.AsSpan().SequenceEqual(
                    File.ReadAllBytes(physicalPath)),
                    $"Canonical bytes changed at '{path}'.");
            }
            else
                Assert.Null(before.Bytes);
        }

        private static JsonElement ParseFirstSkill(JsonObject owner, string skillArray)
        {
            using var document = JsonDocument.Parse(owner[skillArray]!.AsArray()[0]!.ToJsonString());
            return document.RootElement.Clone();
        }

        internal void AssertPublicationPlanBindsExpectedRoots(object plan)
        {
            var afterImages = Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonObject>>(
                ReadRequiredProperty(plan, "OwnerCompanionAfterImages"));
            if (!UsesFinalAfterImage)
            {
                Assert.Empty(afterImages);
                return;
            }

            var expectedRoots = ExpectedCommonPlanPublicationRoots(FinalSource);
            Assert.Equal(expectedRoots.Keys.OrderBy(static path => path),
                afterImages.Keys.OrderBy(static path => path));
            foreach (var (path, expected) in expectedRoots)
            {
                Assert.True(afterImages.TryGetValue(path, out var actual));
                Assert.Equal(expected.ToJsonString(), actual!.ToJsonString());
            }
        }

        internal void AssertPublicationFinalRootScenario()
        {
            if (!Scenario.Publication)
                return;

            if (!UsesFinalAfterImage)
            {
                Assert.NotEqual(
                    SelectedSkillRoot(CurrentSource).ToJsonString(),
                    SelectedSkillRoot(FinalSource).ToJsonString());
                return;
            }
            var currentRoots = ExpectedPublicationRoots(CurrentSource);
            var finalRoots = ExpectedPublicationRoots(FinalSource);
            if (Scenario.ExpectedValid)
            {
                Assert.Equal(currentRoots.Keys.OrderBy(static path => path),
                    finalRoots.Keys.OrderBy(static path => path));
                if (Scenario.Name.StartsWith("touched_", StringComparison.Ordinal))
                {
                    Assert.Contains(finalRoots.Keys,
                        path => !JsonNode.DeepEquals(currentRoots[path], finalRoots[path]));
                    var currentSkill = Skill(
                        CurrentSource,
                        Scenario.SourceOwner,
                        Scenario.SourceSkillArray).DeepClone().AsObject();
                    var finalSkill = Skill(
                        FinalSource,
                        Scenario.SourceOwner,
                        Scenario.SourceSkillArray).DeepClone().AsObject();
                    Assert.NotEqual(
                        currentSkill["displayName"]!.GetValue<string>(),
                        finalSkill["displayName"]!.GetValue<string>());
                    currentSkill.Remove("displayName");
                    finalSkill.Remove("displayName");
                    Assert.True(JsonNode.DeepEquals(currentSkill, finalSkill));
                }
                else
                {
                    foreach (var path in finalRoots.Keys)
                        Assert.True(JsonNode.DeepEquals(currentRoots[path], finalRoots[path]));
                }
                return;
            }

            Assert.Equal(CapabilityFailureBoundary.Exporter, Scenario.ExpectedBoundary);
            Assert.NotNull(Scenario.ExpectedCode);
            if (Scenario.Name is "duplicate_or_confusable_final_skill_row_rejects" or
                "confusable_final_skill_sibling_rejects")
            {
                Assert.True(finalRoots.Keys.ToHashSet(StringComparer.Ordinal)
                    .IsSupersetOf(currentRoots.Keys));
                Assert.Equal(currentRoots.Count + 1, finalRoots.Count);
            }
            else
            {
                Assert.Equal(currentRoots.Keys.OrderBy(static path => path),
                    finalRoots.Keys.OrderBy(static path => path));
            }
            Assert.Contains(finalRoots.Keys,
                path => !currentRoots.TryGetValue(path, out var currentRoot) ||
                        !string.Equals(
                            currentRoot.ToJsonString(),
                            finalRoots[path].ToJsonString(),
                            StringComparison.Ordinal));
        }

        internal void AssertCachedPublicationPlanBinding(
            AcceptedMechanicsPlanBinding binding,
            object acceptedBinding)
        {
            AssertAcceptedBindingMatchesCommonBinding(acceptedBinding, binding);
            var woundInput = Assert.IsType<WoundAcceptedTurnInput>(binding.WoundInput);
            var cached = woundInput.Binding;
            AssertAcceptedBindingMatchesWoundBinding(acceptedBinding, cached);
            if (UsesFinalAfterImage)
            {
                var expectedFinalRoots = ExpectedPublicationRoots(FinalSource);
                foreach (var (path, finalRoot) in expectedFinalRoots)
                {
                    Assert.True(binding.BeforeImages.TryGetValue(path, out var before));
                    Assert.True(before!.Existed);
                    var canonicalBeforeBytes = File.ReadAllBytes(FileSystem.ResolvePath(path));
                    Assert.Equal(canonicalBeforeBytes, before.Bytes);
                    Assert.NotEqual(string.Empty, finalRoot.ToJsonString());
                }
            }
            else
            {
                var expectedSourcePaths = Scenario.SourceOwner == "player"
                    ? new[]
                    {
                        "game_state/player/skills_active.json",
                        "game_state/player/skills_passive.json"
                    }
                    : new[] { SelectedSkillRootPath };
                foreach (var path in expectedSourcePaths)
                {
                    Assert.True(binding.BeforeImages.TryGetValue(path, out var before));
                    Assert.True(before!.Existed);
                    Assert.Equal(
                        File.ReadAllBytes(FileSystem.ResolvePath(path)),
                        before.Bytes);
                }
            }
        }

        private static void AssertAcceptedBindingMatchesCommonBinding(
            object acceptedBinding,
            AcceptedMechanicsPlanBinding commonBinding)
        {
            foreach (var property in new[] { "SessionId", "RequestId", "SnapshotToken", "Realm", "Turn" })
            {
                Assert.Equal(
                    ReadRequiredProperty(acceptedBinding, property),
                    ReadRequiredProperty(commonBinding, property));
            }
        }

        private static void AssertAcceptedBindingMatchesWoundBinding(
            object acceptedBinding,
            object cachedBinding)
        {
            foreach (var property in new[]
                     {
                         "SessionId", "RequestId", "SnapshotToken", "Realm", "Turn",
                         "AcceptedEventsFingerprint"
                     })
            {
                Assert.Equal(
                    ReadRequiredProperty(acceptedBinding, property),
                    ReadRequiredProperty(cachedBinding, property));
            }

            var expectedEvents = ReadEnumerableProperty(acceptedBinding, "AcceptedEvents").ToArray();
            var actualEvents = ReadEnumerableProperty(cachedBinding, "AcceptedEvents").ToArray();
            Assert.Equal(expectedEvents.Length, actualEvents.Length);
            for (var index = 0; index < expectedEvents.Length; index++)
            {
                foreach (var property in new[] { "EventRef", "Kind", "AuthorityId", "SemanticFingerprint" })
                {
                    Assert.Equal(
                        ReadRequiredProperty(expectedEvents[index], property),
                        ReadRequiredProperty(actualEvents[index], property));
                }
            }
        }

        internal void AssertPublicationExportReadsSelectedRoot(CapabilityProofView result)
        {
            if (!Scenario.ExpectedValid)
                return;

            Assert.NotNull(result.Proof);
            var proof = result.Proof!;
            Assert.Equal(
                SelectedSkillRootPath,
                Assert.IsType<string>(ReadRequiredProperty(proof, "SourcePath")));
            var limits = ReadRequiredProperty(proof, "OperationLimits");
            AssertFullOperationLimits(limits, ExpectedPublicationRecoveryPoints);
        }

        private bool UsesFinalAfterImage =>
            Scenario.Publication &&
            !Scenario.Name.StartsWith("untouched_", StringComparison.Ordinal);

        private int ExpectedPublicationRecoveryPoints =>
            Limits(Capability(Skill(
                UsesFinalAfterImage ? FinalSource : CurrentSource,
                Scenario.SourceOwner,
                Scenario.SourceSkillArray)))["maximumRecoveryPoints"]!.GetValue<int>();

        private string SelectedSkillRootPath => Scenario.SourceOwner switch
        {
            "player" when Scenario.SourceSkillArray == "activeSkills" =>
                "game_state/player/skills_active.json",
            "player" => "game_state/player/skills_passive.json",
            _ => "game_state/npcs/npc_core.json"
        };

        private string SelectedSkillName =>
            Skill(CurrentSource, Scenario.SourceOwner, Scenario.SourceSkillArray)["skillName"]!.GetValue<string>();

        private JsonObject SelectedSkillRoot(JsonObject source)
            => PublicationRoot(source, SelectedSkillRootPath);

        private IReadOnlyDictionary<string, JsonObject> ExpectedPublicationRoots(JsonObject source)
        {
            var roots = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            if (!UsesFinalAfterImage)
                return roots;

            if (Scenario.SourceOwner == "npc")
            {
                roots.Add("game_state/npcs/npc_core.json", PublicationRoot(source, "game_state/npcs/npc_core.json"));
                return roots;
            }

            var currentPlayer = Assert.IsType<JsonObject>(CurrentSource["player"]);
            var finalPlayer = Assert.IsType<JsonObject>(source["player"]);
            var selectedPath = SelectedSkillRootPath;
            foreach (var (arrayName, path) in new[]
                     {
                         ("activeSkills", "game_state/player/skills_active.json"),
                         ("passiveSkills", "game_state/player/skills_passive.json")
                     })
            {
                var current = Assert.IsType<JsonArray>(currentPlayer[arrayName]);
                var final = Assert.IsType<JsonArray>(finalPlayer[arrayName]);
                if (path == selectedPath || !JsonNode.DeepEquals(current, final))
                    roots.Add(path, PublicationRoot(source, path));
            }
            return roots;
        }

        private IReadOnlyDictionary<string, JsonObject> ExpectedCommonPlanPublicationRoots(
            JsonObject source)
        {
            var roots = ExpectedPublicationRoots(source).ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal);
            if (!UsesFinalAfterImage || Scenario.SourceOwner != "npc")
                return roots;

            var npcRoot = roots[NpcCoreChangesContract.NpcCorePath];
            Assert.False(npcRoot.ContainsKey("UpdateNpcTradeInventoryReceipts"));
            var actors = new List<JsonObject>();
            foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
            {
                if (npcRoot[section] is not JsonArray rows)
                    continue;
                actors.AddRange(rows.Select(static row => Assert.IsType<JsonObject>(row)));
            }
            Assert.NotEmpty(actors);
            foreach (var actor in actors)
            {
                Assert.False(actor.ContainsKey("tradeInventoryReceipts"));
                actor["tradeInventoryReceipts"] = new JsonArray();
            }

            return roots;
        }

        private JsonObject PublicationRoot(JsonObject source, string path)
        {
            var player = Assert.IsType<JsonObject>(source["player"]);
            var npc = Assert.IsType<JsonObject>(source["npc"]);
            return path switch
            {
                "game_state/player/skills_active.json" => new JsonObject
                {
                    ["activeSkillChanges"] = player["activeSkills"]!.DeepClone()
                },
                "game_state/player/skills_passive.json" => new JsonObject
                {
                    ["passiveSkillChanges"] = player["passiveSkills"]!.DeepClone()
                },
                _ => CreateProductionNpcCore(npc)
            };
        }

        private JsonObject? ReadRoot(string path)
        {
            var fullPath = FileSystem.ResolvePath(path);
            return File.Exists(fullPath)
                ? JsonNode.Parse(File.ReadAllText(fullPath))!.AsObject()
                : null;
        }

        private static JsonObject CreateGuaranteedRoute(CapabilityScenario scenario) => new()
        {
            ["routeId"] = RouteId,
            ["displayName"] = "Guaranteed canonical care",
            ["visibility"] = "known_to_player",
            ["mode"] = "guaranteed",
            ["requirements"] = new JsonArray(
                new JsonObject
                {
                    ["kind"] = "source_capability",
                    ["capabilityRef"] = CapabilityRef,
                    ["actorRole"] = scenario.ActorRole
                },
                new JsonObject
                {
                    ["kind"] = "skill_tier",
                    ["capabilityRef"] = scenario.SkillId,
                    ["minimumTier"] = 2,
                    ["actorRole"] = scenario.ActorRole
                }),
            ["resourcePolicy"] = new JsonObject
            {
                ["reserveBeforeResolution"] = true,
                ["consumeOn"] = new JsonArray("success"),
                ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"),
                ["mutations"] = new JsonArray()
            },
            ["resolution"] = new JsonObject
            {
                ["capabilityRef"] = CapabilityRef,
                ["actorRole"] = scenario.ActorRole
            },
            ["outcomes"] = new JsonArray(new JsonObject
            {
                ["category"] = "success",
                ["result"] = new JsonArray(new JsonObject { ["kind"] = "stabilize" })
            }),
            ["interruption"] = null
        };

        private static void WriteCanonicalSkillSources(FileSystemManager fileSystem, JsonObject source)
        {
            var player = Assert.IsType<JsonObject>(source["player"]);
            var npc = Assert.IsType<JsonObject>(source["npc"]);
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/player/skills_active.json"),
                new JsonObject { ["activeSkillChanges"] = player["activeSkills"]!.DeepClone() }.ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/player/skills_passive.json"),
                new JsonObject { ["passiveSkillChanges"] = player["passiveSkills"]!.DeepClone() }.ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/npcs/npc_core.json"),
                CreateProductionNpcCore(npc).ToJsonString());
        }

        private static JsonObject CreateProductionNpcCore(JsonObject npc)
        {
            var actorId = npc["ownerId"]!.GetValue<string>();
            var actor = MortalActorTestFixtures.CreateActor(
                actorId,
                "loc_field_clinic_001",
                "Capability authority field clinic");
            actor["displayName"] = "Field medic";
            actor["activeSkills"] = npc["activeSkills"]!.DeepClone();
            actor["passiveSkills"] = npc["passiveSkills"]!.DeepClone();
            return new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(actor)
            };
        }
    }
}
