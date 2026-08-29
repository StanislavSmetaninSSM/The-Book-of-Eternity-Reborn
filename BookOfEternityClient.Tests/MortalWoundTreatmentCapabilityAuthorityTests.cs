using System.Collections;
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
        "touched_player_active_skill_reads_exact_final_after_image",
        "touched_player_passive_skill_reads_exact_final_after_image",
        "touched_npc_skill_reads_exact_final_after_image",
        "unchanged_final_source_reexports_equal_proof",
        "removed_final_skill_rejects",
        "retired_final_skill_rejects",
        "changed_final_skill_id_rejects",
        "changed_final_capability_ref_rejects",
        "changed_final_domain_rejects",
        "changed_final_operation_limits_reject",
        "duplicate_or_confusable_final_skill_row_rejects",
        "confusable_final_skill_sibling_rejects",
        "stale_final_source_rejects",
        "publication_export_accepts_no_detached_json_or_publication_intent"
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
        AssertScenarioSpecificCurrentSemantics(fixture, scenario, result);
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
    [InlineData("untouched_player_skill_reads_current_lease_bound_root")]
    [InlineData("touched_player_active_skill_reads_exact_final_after_image")]
    [InlineData("touched_npc_skill_reads_exact_final_after_image")]
    [InlineData("unchanged_final_source_reexports_equal_proof")]
    public void PublicationPlan_NormalizerPublishesTheCachedPlanWithExactSkillRootAfterImages(
        string name)
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(name, publication: true));

        var flow = ComposePublicationFlow(fixture);
        var beforeImages = fixture.CaptureGovernedPublicationBeforeImages(flow.Plan);
        var published = PublishCachedAcceptedPlan(fixture);

        Assert.Same(flow.Plan, published);
        fixture.AssertPublicationPublishedExactSkillRoots(flow.Plan, beforeImages);
    }

    [Fact]
    public void PublicationPlan_StaleBeforeImageRejectsBeforeAnyGovernedSkillRootWrite()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "touched_player_active_skill_reads_exact_final_after_image", publication: true));

        var flow = ComposePublicationFlow(fixture);
        var beforeImages = fixture.CaptureGovernedPublicationBeforeImages(flow.Plan);
        fixture.MutateGovernedRootAfterComposition();
        var staleImages = fixture.CaptureGovernedPublicationBeforeImages(flow.Plan);
        var beforeBytes = beforeImages[fixture.SelectedPublicationRootPath].Bytes;
        var staleBytes = staleImages[fixture.SelectedPublicationRootPath].Bytes;
        Assert.NotNull(beforeBytes);
        Assert.NotNull(staleBytes);
        Assert.False(beforeBytes.AsSpan().SequenceEqual(staleBytes));

        Assert.ThrowsAny<Exception>(() => PublishCachedAcceptedPlan(fixture));
        fixture.AssertGovernedSkillRootsMatchBeforeImages(staleImages);
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
        var beforeImages = fixture.CaptureGovernedPublicationBeforeImages(flow.Plan);
        fault.Arm(fixture.SelectedPublicationRootPath);

        Assert.ThrowsAny<Exception>(() => PublishCachedAcceptedPlan(fixture));
        Assert.True(fault.Fired);
        fault.AssertFiredAfterAnEarlierPlanWrite(flow.Plan);
        fixture.AssertGovernedSkillRootsMatchBeforeImages(beforeImages);
    }

    [Fact]
    public void PublicationPlan_ExactRetryReusesTheCachedPlanAndDivergentCoordinatesRejectBeforePublication()
    {
        using var fixture = CapabilityAuthorityFixture.Create(DescribeScenario(
            "touched_player_active_skill_reads_exact_final_after_image", publication: true));

        var first = ComposePublicationFlow(fixture);
        var retry = ComposePublicationFlow(fixture);
        Assert.Same(first.Plan, retry.Plan);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var cachedRetry));
        Assert.True(cachedRetry.Success, DescribeIssues(cachedRetry.Issues));
        Assert.Same(first.Plan, cachedRetry.Plan);
        var beforeImages = fixture.CaptureGovernedPublicationBeforeImages(first.Plan);

        Assert.ThrowsAny<Exception>(() => ComposePublicationFlow(
            fixture, "capability_authority_operation_conflict_002"));
        fixture.AssertGovernedSkillRootsMatchBeforeImages(beforeImages);
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
        if (scenario.ExpectedValid)
        {
            Assert.NotNull(flow.SealedCurrent.Proof);
            Assert.NotNull(result.Proof);
            Assert.Equal(
                ReadRequiredProperty(flow.SealedCurrent.Proof!, "SourceSemanticFingerprint"),
                ReadRequiredProperty(result.Proof!, "SourceSemanticFingerprint"));
            Assert.Equal(
                ReadRequiredProperty(flow.SealedCurrent.Proof!, "ProofFingerprint"),
                ReadRequiredProperty(result.Proof!, "ProofFingerprint"));
        }
        return result;
    }

    private static PublicationFlow ComposePublicationFlow(
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
        var request = PrepareSealedGuaranteedRequest(fixture, acceptedState, sealedCurrent, operationKey);
        var resolution = ResolveSealedGuaranteedAttempt(fixture, acceptedState, request);
        fixture.AssertPublicationFinalRootScenario();
        Assert.NotNull(accepted.Binding);
        var plan = ComposeT070PlanAndPeek(
            fixture, acceptedState, accepted.Binding!, request, resolution);
        return new PublicationFlow(acceptedState, coordinates, sealedCurrent, plan);
    }

    private static AcceptedMechanicsPlan PublishCachedAcceptedPlan(CapabilityAuthorityFixture fixture) =>
        new CanonicalStateNormalizer(
                fixture.FileSystem,
                NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(fixture.Lease)
            .NormalizeAcceptedMechanicsAsync(backups: null)
            .GetAwaiter()
            .GetResult()
        ?? throw new Xunit.Sdk.XunitException(
            "The common accepted-plan normalizer returned no cached publication plan.");

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
        var proposal = fixture.CreatePublicationProposal();
        fixture.AssertPublicationProposalIsNotFinalPlanAuthority(proposal);
        var beforeComposeBytes = fixture.CapturePublicationCanonicalBytes();
        var composed = Invoke(compose, new object?[]
        {
            fixture.FileSystem,
            fixture.Lease,
            proposal,
            acceptedState,
            request,
            resolution
        });
        fixture.AssertPublicationComposeDidNotWriteCanonicalRoots(beforeComposeBytes);
        var publicationPlan = ReadValidTypedResult(composed, "Plan", "T070 accepted publication");
        Assert.NotSame(proposal, publicationPlan);
        Assert.NotEqual(proposal.GetType(), publicationPlan.GetType());
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var cachedBinding,
            out var cached));
        Assert.True(cached.Success, DescribeIssues(cached.Issues));
        Assert.Same(publicationPlan, cached.Plan);
        fixture.AssertCachedPublicationPlanBinding(cachedBinding, acceptedBinding);
        fixture.AssertPublicationPlanBindsExpectedRoots(publicationPlan, proposal);
        return publicationPlan;
    }

    private static CapabilityProofView InvokeCapabilityExporter(
        string methodName,
        object acceptedState,
        object coordinates,
        string capabilityRef,
        string actorRole,
        object? publicationPlan = null)
    {
        var authorityType = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentCapabilityAuthority",
            throwOnError: false,
            ignoreCase: false);

        Assert.NotNull(authorityType);

        var method = Assert.Single(
            authorityType.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
            candidate =>
                candidate.Name == methodName &&
                candidate.GetParameters().Length == (publicationPlan is null ? 4 : 5));

        var parameters = method.GetParameters();
        Assert.Equal(acceptedState.GetType(), parameters[0].ParameterType);
        Assert.Equal(coordinates.GetType(), parameters[1].ParameterType);
        Assert.Equal(typeof(string), parameters[2].ParameterType);
        Assert.Equal(typeof(string), parameters[3].ParameterType);
        if (publicationPlan is not null)
        {
            Assert.Equal(publicationPlan.GetType(), parameters[4].ParameterType);
        }

        var arguments = publicationPlan is null
            ? new[] { acceptedState, coordinates, (object)capabilityRef, actorRole }
            : new[] { acceptedState, coordinates, (object)capabilityRef, actorRole, publicationPlan };
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
        ["player"] = CreateSkillOwner("player_current"),
        ["npc"] = CreateSkillOwner("npc_field_medic_01")
    };

    private static JsonObject CreateSkillOwner(string ownerId) => new()
    {
        ["ownerId"] = ownerId,
        ["activeSkills"] = new JsonArray(CreateSkill("active")),
        ["passiveSkills"] = new JsonArray(CreateSkill("passive"))
    };

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
                Limits(capability)["maximumRecoveryPoints"] = long.MaxValue;
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
                Limits(capability)["maximumRecoveryPoints"] = 2;
                break;
            case "touched_player_active_skill_reads_exact_final_after_image":
            case "touched_player_passive_skill_reads_exact_final_after_image":
            case "touched_npc_skill_reads_exact_final_after_image":
                // T070 must record an exact selected final root even when the
                // companion change is semantically neutral. The publication proof is
                // required to equal the proof sealed before composition.
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
                break;
            case "confusable_final_skill_sibling_rejects":
                OtherSkill(fixture, scenario)["skillId"] =
                    scenario.SkillId.Replace("i", "і", StringComparison.Ordinal);
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
    }

    private static void AssertProofShape(object proof, CapabilityScenario scenario)
    {
        Assert.False(proof is JsonNode or JsonElement or JsonDocument);
        var properties = proof.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetIndexParameters().Length == 0)
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
        AssertFullOperationLimits(
            ReadRequiredProperty(proof, "OperationLimits"),
            scenario.Name.StartsWith("untouched_", StringComparison.Ordinal) ||
            scenario.Name == "source_semantic_change_changes_proof" ? 2 : 1);
        Assert.NotEqual(string.Empty, Assert.IsType<string>(ReadRequiredProperty(proof, "SourceSemanticFingerprint")));
        Assert.NotEqual(string.Empty, Assert.IsType<string>(ReadRequiredProperty(proof, "ProofFingerprint")));
        Assert.False(
            JsonSerializer.Serialize(proof, proof.GetType()).Contains("mortalWoundTreatmentCapabilities", StringComparison.Ordinal),
            $"{scenario.Name} leaked source JSON through the detached proof.");
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
                    var baselineProof = Assert.IsType<string>(ReadRequiredProperty(
                        baseline.Proof!, "ProofFingerprint"));
                    var currentProof = Assert.IsType<string>(ReadRequiredProperty(
                        result.Proof!, "ProofFingerprint"));
                    if (scenario.Name == "source_semantic_change_changes_proof")
                    {
                        Assert.NotEqual(baselineSource, currentSource);
                        Assert.NotEqual(baselineProof, currentProof);
                    }
                    else
                    {
                        Assert.Equal(baselineSource, currentSource);
                        Assert.Equal(baselineProof, currentProof);
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
            "touched_player_active_skill_reads_exact_final_after_image" or
            "changed_final_skill_id_rejects" or
            "duplicate_or_confusable_final_skill_row_rejects" or
            "confusable_final_skill_sibling_rejects" => ("player", "activeSkills"),
            "player_passive_skill_exports_physical_capability" or
            "touched_player_passive_skill_reads_exact_final_after_image" => ("player", "passiveSkills"),
            "target_owned_combatant_requires_promotion" => ("player", "activeSkills"),
            "npc_passive_skill_exports_physical_capability" => ("npc", "passiveSkills"),
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
            "changed_final_operation_limits_reject" =>
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
        object Plan);

    private sealed record PublicationBeforeImage(bool Existed, byte[]? Bytes);

    private sealed class PublicationFailureInjection
    {
        private string? _path;

        private readonly List<string> _observedPaths = [];

        internal bool Fired { get; private set; }

        internal void Arm(string path)
        {
            _path = path;
            Fired = false;
            _observedPaths.Clear();
        }

        internal Task BeforeCanonicalMutationAsync(string path)
        {
            _observedPaths.Add(path);
            if (!Fired && string.Equals(path, _path, StringComparison.Ordinal))
            {
                Fired = true;
                return Task.FromException(new IOException(
                    $"Injected accepted-plan publication failure at '{path}'."));
            }

            return Task.CompletedTask;
        }

        internal void AssertFiredAfterAnEarlierPlanWrite(object plan)
        {
            Assert.True(Fired);
            Assert.NotNull(_path);
            var firedIndex = _observedPaths.FindIndex(path =>
                string.Equals(path, _path, StringComparison.Ordinal));
            Assert.True(firedIndex > 0,
                "The injected failure must occur after at least one publication mutation.");
            var planWriteSet = ReadEnumerableProperty(plan, "TouchedPaths")
                .Select(Assert.IsType<string>)
                .Concat(Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonObject>>(
                    ReadRequiredProperty(plan, "OwnerCompanionAfterImages")).Keys)
                .ToHashSet(StringComparer.Ordinal);
            Assert.Contains(
                _observedPaths.Take(firedIndex),
                path => !string.Equals(path, _path, StringComparison.Ordinal) &&
                        planWriteSet.Contains(path));
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
            Lease = lease;
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

        internal string Root { get; }
        internal FileSystemManager FileSystem { get; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; }
        internal WoundMaterializationEnvelope Before { get; }
        internal LiveTurnPreparationResult PreparedTurn { get; }
        internal JsonObject ContextRoot { get; }
        internal CapabilityScenario Scenario { get; }
        internal JsonObject CurrentSource { get; }
        internal JsonObject FinalSource { get; }

        internal static CapabilityAuthorityFixture Create(
            CapabilityScenario scenario,
            FileSystemManagerHooks? hooks = null)
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
            AssertCanonicalFixtureShape(currentSource);
            AssertCanonicalFixtureShape(finalSource);
            WriteCanonicalSkillSources(fileSystem, currentSource);
            Directory.CreateDirectory(Path.GetDirectoryName(
                fileSystem.ResolvePath("game_state/world/world_time.json"))!);
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/world/world_time.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["currentTimeInMinutes"] = 1_260
                }.ToJsonString());
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
            woundRoot["treatment"]!["routes"] = new JsonArray(
                CreateGuaranteedRoute(scenario));
            woundRoot["treatment"]!["knownRouteIds"] = new JsonArray(RouteId);
            var parsed = WoundMaterializationContract.Parse(woundRoot.ToJsonString(), "wound");
            Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
            if (combatTarget)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(
                    fileSystem.ResolvePath(carrierPath))!);
                var combatant = scenario.TargetKind == "combatant_member"
                    ? new JsonObject
                    {
                        ["combatantId"] = "combatant_group_01",
                        ["isGroup"] = true,
                        ["members"] = new JsonArray(new JsonObject
                        {
                            ["memberId"] = scenario.TargetId,
                            ["activeWounds"] = new JsonArray(woundRoot.DeepClone())
                        })
                    }
                    : new JsonObject
                    {
                        ["combatantId"] = scenario.TargetId,
                        ["activeWounds"] = new JsonArray(woundRoot.DeepClone())
                    };
                File.WriteAllText(
                    fileSystem.ResolvePath(carrierPath),
                    new JsonObject
                    {
                        [scenario.TargetKind == "combatant_member" ? "alliesData" : "enemiesData"] =
                            new JsonArray(combatant)
                    }.ToJsonString());
            }
            else
            {
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/player/wounds.json"),
                    WoundContractTestData.CreatePlayerCarrier(woundRoot).ToJsonString());
            }
            File.WriteAllText(
                fileSystem.ResolvePath(WoundIdentityState.StatePath),
                WoundContractTestData.CreateIdentityIndex(
                    WoundContractTestData.CreateIdentityEntry(
                        woundId: WoundId,
                        ownerKind: combatTarget ? scenario.TargetKind : "player",
                        ownerId: combatTarget ? scenario.TargetId : "player_current",
                        carrierPath: combatTarget ? carrierPath : "game_state/player/wounds.json")).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath(WoundHistoryState.HistoryPath),
                WoundContractTestData.CreateHistory(WoundContractTestData.CreateTransition()).ToJsonString());
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

        public void Dispose()
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Directory.Delete(Root, recursive: true);
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
                    Limits(capability)["maximumRecoveryPoints"] = 2;
                    break;
                case "stale_final_source_rejects":
                    selected["active"] = false;
                    break;
                case "duplicate_or_confusable_final_skill_row_rejects":
                    OtherSkill(rows, Scenario)["skillId"] = selected["skillId"]!.DeepClone();
                    break;
                case "confusable_final_skill_sibling_rejects":
                    OtherSkill(rows, Scenario)["skillId"] =
                        Scenario.SkillId.Replace("i", "і", StringComparison.Ordinal);
                    break;
                case "changed_final_skill_id_rejects":
                    // Existing composition selects an update by exact skillId/name;
                    // replacement identity must therefore be an explicit remove/add,
                    // not a fictional in-place ID rewrite.
                    selected["skillId"] = "skill_after_image_changed_01";
                    selected["skillName"] = "After-image Field Medicine";
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

        internal void AssertProductionValidSkillShapes()
        {
            var player = Assert.IsType<JsonObject>(CurrentSource["player"]);
            var npc = Assert.IsType<JsonObject>(CurrentSource["npc"]);
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
                Assert.IsType<JsonArray>(npcRoot!["NPCs"])));
            Assert.Equal("npc_field_medic_01", persistedNpc["npcId"]!.GetValue<string>());
            Assert.True(ValidationService.IsProductionValidMortalActiveSkill(
                ParseFirstSkill(persistedNpc, "activeSkills")));
            Assert.True(ValidationService.IsProductionValidMortalPassiveSkill(
                ParseFirstSkill(persistedNpc, "passiveSkills")));
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

        internal IReadOnlyDictionary<string, byte[]> CapturePublicationCanonicalBytes() =>
            new[]
            {
                "game_state/player/skills_active.json",
                "game_state/player/skills_passive.json",
                "game_state/npcs/npc_core.json"
            }.ToDictionary(
                static path => path,
                path => File.ReadAllBytes(FileSystem.ResolvePath(path)),
                StringComparer.Ordinal);

        internal void AssertPublicationComposeDidNotWriteCanonicalRoots(
            IReadOnlyDictionary<string, byte[]> beforeComposeBytes)
        {
            foreach (var (path, expectedBytes) in beforeComposeBytes)
                Assert.Equal(expectedBytes, File.ReadAllBytes(FileSystem.ResolvePath(path)));
        }

        internal string SelectedPublicationRootPath => SelectedSkillRootPath;

        internal IReadOnlyDictionary<string, PublicationBeforeImage>
            CaptureGovernedPublicationBeforeImages(object plan)
        {
            var touchedPaths = ReadEnumerableProperty(plan, "TouchedPaths")
                .Select(Assert.IsType<string>)
                .Concat(Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonObject>>(
                    ReadRequiredProperty(plan, "OwnerCompanionAfterImages")).Keys)
                .Append(SelectedSkillRootPath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.NotEmpty(touchedPaths);

            return touchedPaths.ToDictionary(
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

        internal void AssertPublicationPublishedExactSkillRoots(
            object plan,
            IReadOnlyDictionary<string, PublicationBeforeImage> beforeImages)
        {
            var afterImages = Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonObject>>(
                ReadRequiredProperty(plan, "OwnerCompanionAfterImages"));
            var expectedRoots = UsesFinalAfterImage
                ? ExpectedPublicationRoots(FinalSource)
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

        internal void MutateGovernedRootAfterComposition()
        {
            var root = JsonNode.Parse(File.ReadAllText(
                FileSystem.ResolvePath(SelectedSkillRootPath)))!.AsObject();
            root["_stalePublicationProbe"] = "mutated-after-t070-compose";
            File.WriteAllText(FileSystem.ResolvePath(SelectedSkillRootPath), root.ToJsonString());
        }

        internal void AssertGovernedSkillRootsMatchBeforeImages(
            IReadOnlyDictionary<string, PublicationBeforeImage> beforeImages)
        {
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

        internal void AssertPublicationPlanBindsExpectedRoots(
            object plan,
            GameResponse proposal)
        {
            AssertPublicationProposalIsNotFinalPlanAuthority(proposal);
            var afterImages = Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonObject>>(
                ReadRequiredProperty(plan, "OwnerCompanionAfterImages"));
            if (!UsesFinalAfterImage)
            {
                Assert.Empty(afterImages);
                return;
            }

            var expectedRoots = ExpectedPublicationRoots(FinalSource);
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
                foreach (var path in finalRoots.Keys)
                    Assert.Equal(currentRoots[path].ToJsonString(), finalRoots[path].ToJsonString());
                return;
            }

            Assert.Equal(CapabilityFailureBoundary.Exporter, Scenario.ExpectedBoundary);
            Assert.NotNull(Scenario.ExpectedCode);
            Assert.Equal(currentRoots.Keys.OrderBy(static path => path),
                finalRoots.Keys.OrderBy(static path => path));
            Assert.Contains(finalRoots.Keys,
                path => !string.Equals(
                    currentRoots[path].ToJsonString(),
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
                Assert.DoesNotContain(SelectedSkillRootPath, binding.BeforeImages.Keys);
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
                _ => new JsonObject
                {
                    ["NPCs"] = new JsonArray(new JsonObject
                    {
                        ["npcId"] = npc["ownerId"]!.DeepClone(),
                        ["activeSkills"] = npc["activeSkills"]!.DeepClone(),
                        ["passiveSkills"] = npc["passiveSkills"]!.DeepClone()
                    })
                }
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
                new JsonObject
                {
                    ["NPCs"] = new JsonArray(new JsonObject
                    {
                        ["npcId"] = npc["ownerId"]!.DeepClone(),
                        ["activeSkills"] = npc["activeSkills"]!.DeepClone(),
                        ["passiveSkills"] = npc["passiveSkills"]!.DeepClone()
                    })
                }.ToJsonString());
        }
    }
}
