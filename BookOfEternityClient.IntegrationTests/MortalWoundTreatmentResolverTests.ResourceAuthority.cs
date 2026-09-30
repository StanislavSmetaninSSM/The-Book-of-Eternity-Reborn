using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// T068-A coverage for the production-only Mortal wound-treatment resource boundary.
/// Tests obtain every coordinate, bundle, mode authority, witness, and fingerprint from
/// the current lease-bound accepted state; no resource authority is test-constructed.
/// </summary>
public sealed partial class MortalWoundTreatmentResolverTests
{
    private const string TreatmentResourceComposerTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentResourceComposer";

    [Fact]
    public void ResourcePreparation_FirstCourseUnsatisfiedRequirementRejectsWithoutReservation()
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_unsatisfied_dose_requirement_has_no_reservation",
                "course"),
            removeOwnedComplicationBeforeReduction: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var probe = InspectCourseMode(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            probe.AcceptedState);
        var coordinates = Assert.IsType<MortalWoundTreatmentAttemptCoordinates>(
            probe.Coordinates);
        var courseMode = Assert.IsType<MortalWoundCourseModeAuthority>(
            AssertReadyCourseModeAuthority(probe.Result, scenario));
        var bundleResult =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForCourseMilestone(
                acceptedState,
                coordinates,
                probe.Before,
                probe.History,
                courseMode);

        Assert.Equal("Unsatisfied", bundleResult.Status);
        Assert.Empty(bundleResult.Issues);
        var bundle = Assert.IsType<MortalWoundTreatmentRequirementAuthorityBundle>(
            bundleResult.Authority);
        Assert.Equal("Unsatisfied", bundle.CourseRequirementStatus);
        Assert.NotEmpty(bundle.Scopes.SelectMany(static scope => scope.FailureWitnesses));

        var preparation = Invoke(
            RequireResourcePreparationMethod(
                "PrepareCourse",
                typeof(MortalWoundTreatmentAcceptedStateAuthority),
                typeof(MortalWoundTreatmentAttemptCoordinates),
                typeof(WoundMaterializationEnvelope),
                typeof(MortalWoundTreatmentRequirementAuthorityBundle),
                typeof(MortalWoundCourseModeAuthority)),
            new object?[]
            {
                acceptedState,
                coordinates,
                probe.Before,
                bundle,
                courseMode
            });

        AssertClosedProperties(preparation, new[] { "IsValid", "Issues", "Authority" });
        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(preparation, "IsValid")));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(preparation, "Issues"))
            .Select(Assert.IsType<ValidationIssue>));
        Assert.Null(ReadPropertyAllowingNull(preparation, "Authority"));
    }

    [Fact]
    public void ResourcePreparation_GuaranteedWithoutQuantitiesReturnsStableNotRequiredAuthority()
    {
        var scenario = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var before = fixture.ReadCurrentWound();
        var coordinatesResult = MortalWoundTreatmentPlanner.CreateAttemptCoordinates(
            acceptedState,
            before,
            scenario.OperationKey,
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.True(coordinatesResult.IsValid, DescribeIssues(coordinatesResult.Issues));
        var coordinates = Assert.IsType<MortalWoundTreatmentAttemptCoordinates>(
            coordinatesResult.Coordinates);
        var route = Assert.IsType<MortalWoundGuaranteedRouteDefinition>(Assert.Single(
            acceptedState.TreatmentDefinition.Routes,
            candidate => string.Equals(
                candidate.RouteId,
                scenario.RouteId,
                StringComparison.Ordinal)));
        var bundleResult =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForGuaranteed(
                acceptedState,
                coordinates,
                before);
        Assert.True(bundleResult.IsValid, DescribeIssues(bundleResult.Issues));
        var bundle = Assert.IsType<MortalWoundTreatmentRequirementAuthorityBundle>(
            bundleResult.Authority);
        var proofResult = MortalWoundTreatmentCapabilityAuthority.ExportCurrent(
            acceptedState,
            coordinates,
            route.Resolution.CapabilityRef,
            route.Resolution.ActorRole);
        Assert.True(proofResult.IsValid, DescribeIssues(proofResult.Issues));
        var proof = Assert.IsType<MortalWoundTreatmentCapabilityProof>(proofResult.Proof);
        var method = RequireResourcePreparationMethod(
            "PrepareGuaranteed",
            typeof(MortalWoundTreatmentAcceptedStateAuthority),
            typeof(MortalWoundTreatmentAttemptCoordinates),
            typeof(WoundMaterializationEnvelope),
            typeof(MortalWoundTreatmentRequirementAuthorityBundle),
            typeof(MortalWoundTreatmentCapabilityProof));

        var first = Invoke(method, new object?[]
        {
            acceptedState,
            coordinates,
            before,
            bundle,
            proof
        });
        var retry = Invoke(method, new object?[]
        {
            acceptedState,
            coordinates,
            before,
            bundle,
            proof
        });

        var firstAuthority = AssertValidResourcePreparation(first);
        var retryAuthority = AssertValidResourcePreparation(retry);
        Assert.Equal(CanonicalValue(firstAuthority), CanonicalValue(retryAuthority));
        AssertClosedProperties(firstAuthority, new[]
        {
            "ReservationDisposition", "ReservationId", "CoordinatesFingerprint",
            "AcceptedStateFingerprint", "RouteFingerprint", "CourseId",
            "CourseMilestoneOrdinal", "CourseCoordinateFingerprint",
            "RequirementAuthorityFingerprint", "Policy", "Claims",
            "AuthorityFingerprint"
        });
        Assert.Equal("not_required", ReadRequiredProperty(
            firstAuthority,
            "ReservationDisposition"));
        Assert.Null(ReadPropertyAllowingNull(firstAuthority, "ReservationId"));
        Assert.Equal(coordinates.CoordinatesFingerprint, ReadRequiredProperty(
            firstAuthority,
            "CoordinatesFingerprint"));
        Assert.Equal(coordinates.AcceptedStateFingerprint, ReadRequiredProperty(
            firstAuthority,
            "AcceptedStateFingerprint"));
        Assert.Equal(bundle.RouteFingerprint, ReadRequiredProperty(
            firstAuthority,
            "RouteFingerprint"));
        Assert.Null(ReadPropertyAllowingNull(firstAuthority, "CourseId"));
        Assert.Null(ReadPropertyAllowingNull(firstAuthority, "CourseMilestoneOrdinal"));
        Assert.Null(ReadPropertyAllowingNull(firstAuthority, "CourseCoordinateFingerprint"));
        Assert.Equal(bundle.AuthorityFingerprint, ReadRequiredProperty(
            firstAuthority,
            "RequirementAuthorityFingerprint"));
        var policy = Assert.IsType<MortalWoundTreatmentResourcePolicy>(
            ReadRequiredProperty(firstAuthority, "Policy"));
        Assert.Equal(
            route.ResourcePolicy.ReserveBeforeResolution,
            policy.ReserveBeforeResolution);
        Assert.Equal(
            route.ResourcePolicy.ConsumeOn.ToArray(),
            policy.ConsumeOn.ToArray());
        Assert.Equal(
            route.ResourcePolicy.RefundOn.ToArray(),
            policy.RefundOn.ToArray());
        Assert.Equal(
            route.ResourcePolicy.Mutations.ToArray(),
            policy.Mutations.ToArray());
        Assert.Empty(AsObjects(ReadRequiredProperty(firstAuthority, "Claims")));
        AssertAuthorityFingerprint(ReadRequiredProperty(firstAuthority, "AuthorityFingerprint"));
    }

    [Theory]
    [InlineData(
        true,
        "mortal_wound_treatment_resource_requirement_authority_invalid")]
    [InlineData(
        false,
        "mortal_wound_treatment_resource_mode_authority_invalid")]
    public void ResourcePreparation_MissingClosedAuthorityRejectsWithTypedIssue(
        bool omitRequirementAuthority,
        string expectedCode)
    {
        var scenario = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var prepared = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);

        var result = MortalWoundTreatmentResourceComposer.PrepareGuaranteed(
            prepared.AcceptedState,
            prepared.Coordinates,
            prepared.Before,
            omitRequirementAuthority ? null! : prepared.RequirementAuthority,
            omitRequirementAuthority ? prepared.CapabilityProof : null!);

        AssertInvalidResourcePreparation(result, expectedCode);
    }

    [Fact]
    public void ResourcePreparation_ProcedureQuantityProducesClosedHeldClaim()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var prepared = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var invocation = InvokePreparedProcedureCheckAuthority(prepared);
        var procedureAuthority = Assert.IsType<MortalWoundProcedureCheckAuthority>(
            AssertValidProcedureCheckAuthority(invocation));

        var result = MortalWoundTreatmentResourceComposer.PrepareProcedure(
            prepared.AcceptedState,
            prepared.Coordinates,
            prepared.Before,
            prepared.RequirementAuthority,
            procedureAuthority);

        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        var authority = Assert.IsType<MortalWoundTreatmentResourceReservationAuthority>(
            result.Authority);
        Assert.Equal("held", authority.ReservationDisposition);
        Assert.False(string.IsNullOrWhiteSpace(authority.ReservationId));
        Assert.Equal(prepared.Coordinates.CoordinatesFingerprint,
            authority.CoordinatesFingerprint);
        Assert.Equal(prepared.Coordinates.AcceptedStateFingerprint,
            authority.AcceptedStateFingerprint);
        Assert.Equal(prepared.RequirementAuthority.RouteFingerprint,
            authority.RouteFingerprint);
        Assert.Equal(prepared.RequirementAuthority.AuthorityFingerprint,
            authority.RequirementAuthorityFingerprint);
        Assert.Null(authority.CourseId);
        Assert.Null(authority.CourseMilestoneOrdinal);
        Assert.Null(authority.CourseCoordinateFingerprint);
        var claim = Assert.Single(authority.Claims);
        AssertImmutableConcreteSurface(claim.GetType(), new[]
        {
            "Scope", "RequirementIndex", "Kind", "AuthorityRef", "Realm",
            "OwnerKind", "OwnerId", "Quantity", "SuccessWitnessFingerprint",
            "ClaimFingerprint"
        });
        var binding = Assert.Single(
            Assert.Single(prepared.RequirementAuthority.Scopes).Bindings,
            static candidate => candidate.RequirementIndex == 0);
        Assert.Equal("common", claim.Scope);
        Assert.Equal(0, claim.RequirementIndex);
        Assert.Equal("item_quantity", claim.Kind);
        Assert.Equal("sterile_thread", claim.AuthorityRef);
        Assert.Equal("mortal_world", claim.Realm);
        Assert.Equal("npc", claim.OwnerKind);
        Assert.Equal("field_medic_01", claim.OwnerId);
        Assert.Equal(1, claim.Quantity);
        Assert.Equal(binding.SuccessWitness.WitnessFingerprint,
            claim.SuccessWitnessFingerprint);
        AssertAuthorityFingerprint(claim.ClaimFingerprint);
        AssertAuthorityFingerprint(authority.AuthorityFingerprint);
        AssertFrozenSequence(authority.Claims, allowEmptyArray: false);
    }

    [Fact]
    public void ResourcePreparation_ProcedurePreservesAuthoredConsumeOrder()
    {
        var source = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        var before = source.Before.DeepClone().AsObject();
        before["treatment"]!["routes"]![0]!["resourcePolicy"]!["consumeOn"] =
            new JsonArray("failed_attempt", "success");
        var scenario = source with { Before = before };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var prepared = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var procedureAuthority = Assert.IsType<MortalWoundProcedureCheckAuthority>(
            AssertValidProcedureCheckAuthority(
                InvokePreparedProcedureCheckAuthority(prepared)));

        var result = MortalWoundTreatmentResourceComposer.PrepareProcedure(
            prepared.AcceptedState,
            prepared.Coordinates,
            prepared.Before,
            prepared.RequirementAuthority,
            procedureAuthority);

        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        Assert.Equal(
            new[] { "failed_attempt", "success" },
            Assert.IsType<MortalWoundTreatmentResourceReservationAuthority>(
                result.Authority).Policy.ConsumeOn);
    }

    [Fact]
    public void ResourcePreparation_FirstCourseClaimsOnlyCommonAndOrdinalOneRequirements()
    {
        var scenario = CreateFirstCourseResourceScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var probe = InspectCourseMode(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            probe.AcceptedState);
        var coordinates = Assert.IsType<MortalWoundTreatmentAttemptCoordinates>(
            probe.Coordinates);
        var courseMode = Assert.IsType<MortalWoundCourseModeAuthority>(
            AssertReadyCourseModeAuthority(probe.Result, scenario));
        Assert.Equal(1, courseMode.MilestoneOrdinal);
        var bundleResult =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForCourseMilestone(
                acceptedState,
                coordinates,
                probe.Before,
                probe.History,
                courseMode);
        Assert.Equal("Satisfied", bundleResult.Status);
        var bundle = Assert.IsType<MortalWoundTreatmentRequirementAuthorityBundle>(
            bundleResult.Authority);

        var result = MortalWoundTreatmentResourceComposer.PrepareCourse(
            acceptedState,
            coordinates,
            probe.Before,
            bundle,
            courseMode);

        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        var authority = Assert.IsType<MortalWoundTreatmentResourceReservationAuthority>(
            result.Authority);
        Assert.Equal("held", authority.ReservationDisposition);
        Assert.Equal(courseMode.CourseId, authority.CourseId);
        Assert.Equal(1, authority.CourseMilestoneOrdinal);
        Assert.Equal(courseMode.CourseCoordinateFingerprint,
            authority.CourseCoordinateFingerprint);
        Assert.Equal(
            new[]
            {
                (Scope: "common", Index: 0, Ref: "sterile_thread"),
                (Scope: "course_milestone", Index: 0, Ref: "antibiotic_dose")
            },
            authority.Claims.Select(static claim =>
                (claim.Scope, claim.RequirementIndex, claim.AuthorityRef)));
        Assert.DoesNotContain(authority.Claims, static claim =>
            claim.AuthorityRef.StartsWith("future_course_dose_", StringComparison.Ordinal));
    }

    [Fact]
    public void ResourcePreparation_ExactRetryReusesReservationWithoutDoubleBooking()
    {
        var scenario = CreateGuaranteedResourceRegistryScenario(
            quantity: 2,
            includeAlternateRoute: false);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var prepared = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);

        var first = InvokePreparedGuaranteedResource(prepared);
        var retry = InvokePreparedGuaranteedResource(prepared);

        Assert.True(first.IsValid, DescribeIssues(first.Issues));
        Assert.True(retry.IsValid, DescribeIssues(retry.Issues));
        Assert.Same(first.Authority, retry.Authority);

        var competing = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey + "_competing",
            scenario.RouteId);
        AssertInvalidResourcePreparation(
            InvokePreparedGuaranteedResource(competing),
            "mortal_wound_treatment_resource_reservation_overbooked");
    }

    [Fact]
    public void ResourcePreparation_DivergentRetryConflicts()
    {
        var scenario = CreateGuaranteedResourceRegistryScenario(
            quantity: 1,
            includeAlternateRoute: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var changed = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey,
            AlternateGuaranteedRouteId);

        var firstResult = InvokePreparedGuaranteedResource(first);
        Assert.True(firstResult.IsValid, DescribeIssues(firstResult.Issues));
        AssertInvalidResourcePreparation(
            InvokePreparedGuaranteedResource(changed),
            "mortal_wound_treatment_resource_reservation_conflict");
    }

    [Fact]
    public void ResourcePreparation_SecondOperationCannotOverbookHeldQuantity()
    {
        var scenario = CreateGuaranteedResourceRegistryScenario(
            quantity: 2,
            includeAlternateRoute: false);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey + "_first",
            scenario.RouteId);
        var second = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey + "_second",
            scenario.RouteId);

        var firstResult = InvokePreparedGuaranteedResource(first);
        Assert.True(firstResult.IsValid, DescribeIssues(firstResult.Issues));
        AssertInvalidResourcePreparation(
            InvokePreparedGuaranteedResource(second),
            "mortal_wound_treatment_resource_reservation_overbooked");
    }

    [Fact]
    public void ResourcePreparation_FirstCourseResourceQuantityClaimsCannotOverbook()
    {
        var scenario = ConfigureCourseCrossScopeResourceQuantity(
            ConfigureCourseConsequenceEnvelope(
                CreateScenario(
                    "course_first_milestone_is_ready_at_inclusive_due_time",
                    "course"),
                removeOwnedComplicationBeforeReduction: true),
            commonQuantity: 5,
            milestoneQuantity: 5);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerHealthForRequirementTest(10);

        var first = PrepareCourseResourceInputs(
            fixture,
            scenario,
            scenario.OperationKey + "_first");
        var firstResult = MortalWoundTreatmentResourceComposer.PrepareCourse(
            first.AcceptedState,
            first.Coordinates,
            first.Before,
            first.RequirementAuthority,
            first.CourseAuthority);

        Assert.True(firstResult.IsValid, DescribeIssues(firstResult.Issues));
        var authority = Assert.IsType<MortalWoundTreatmentResourceReservationAuthority>(
            firstResult.Authority);
        Assert.Equal(
            new[]
            {
                (Scope: "common", Quantity: 5),
                (Scope: "course_milestone", Quantity: 5)
            },
            authority.Claims.Select(static claim => (claim.Scope, claim.Quantity)));
        Assert.All(authority.Claims, static claim =>
        {
            Assert.Equal("resource_quantity", claim.Kind);
            Assert.Equal("health", claim.AuthorityRef);
            Assert.Equal("player", claim.OwnerKind);
            Assert.Equal("player_current", claim.OwnerId);
        });

        var second = PrepareCourseResourceInputs(
            fixture,
            scenario,
            scenario.OperationKey + "_second");
        AssertInvalidResourcePreparation(
            MortalWoundTreatmentResourceComposer.PrepareCourse(
                second.AcceptedState,
                second.Coordinates,
                second.Before,
                second.RequirementAuthority,
                second.CourseAuthority),
            "mortal_wound_treatment_resource_reservation_overbooked");
    }

    [Fact]
    public void ResourcePreparation_ReleasedProcedureAuthorityCannotReserveResources()
    {
        const string guaranteedRouteId = "guaranteed_t068a_after_released_procedure";
        var source = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        var before = source.Before.DeepClone().AsObject();
        var guaranteed = StrictGuaranteedRoute();
        guaranteed["routeId"] = guaranteedRouteId;
        before["treatment"]!["routes"]!.AsArray().Add(guaranteed);
        before["treatment"]!["knownRouteIds"]!.AsArray().Add(guaranteedRouteId);
        var scenario = source with { Before = before };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var prepared = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var procedureAuthority = Assert.IsType<MortalWoundProcedureCheckAuthority>(
            AssertValidProcedureCheckAuthority(
                InvokePreparedProcedureCheckAuthority(prepared)));
        Assert.True(procedureAuthority.ReleaseProvisionalReservations(
            prepared.AcceptedState));

        var rejected = MortalWoundTreatmentResourceComposer.PrepareProcedure(
            prepared.AcceptedState,
            prepared.Coordinates,
            prepared.Before,
            prepared.RequirementAuthority,
            procedureAuthority);

        AssertInvalidResourcePreparation(
            rejected,
            "mortal_wound_treatment_resource_procedure_reservation_invalid");
        var zeroClaim = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey,
            guaranteedRouteId);
        var zeroClaimResult = InvokePreparedGuaranteedResource(zeroClaim);
        Assert.True(zeroClaimResult.IsValid, DescribeIssues(zeroClaimResult.Issues));
        Assert.Equal("not_required", zeroClaimResult.Authority!.ReservationDisposition);
    }

    [Fact]
    public void ResourcePreparation_NotRequiredChangedRetryConflicts()
    {
        var source = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        var before = source.Before.DeepClone().AsObject();
        var alternate = before["treatment"]!["routes"]![0]!.DeepClone().AsObject();
        alternate["routeId"] = AlternateGuaranteedRouteId;
        before["treatment"]!["routes"]!.AsArray().Add(alternate);
        before["treatment"]!["knownRouteIds"]!.AsArray().Add(
            AlternateGuaranteedRouteId);
        var scenario = source with { Before = before };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var changed = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey,
            AlternateGuaranteedRouteId);

        var firstResult = InvokePreparedGuaranteedResource(first);
        Assert.True(firstResult.IsValid, DescribeIssues(firstResult.Issues));
        Assert.Equal("not_required", firstResult.Authority!.ReservationDisposition);
        AssertInvalidResourcePreparation(
            InvokePreparedGuaranteedResource(changed),
            "mortal_wound_treatment_resource_reservation_conflict");
    }

    [Fact]
    public void ResourcePreparation_RollbackNewReleasesOnlyCreatingPreparation()
    {
        var scenario = CreateGuaranteedResourceRegistryScenario(
            quantity: 2,
            includeAlternateRoute: false);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var creating = InvokePreparedGuaranteedResource(
            PrepareGuaranteedResourceInputs(
                fixture,
                scenario.OperationKey + "_creating",
                scenario.RouteId));
        Assert.True(creating.IsValid, DescribeIssues(creating.Issues));

        Assert.True(RollbackNewResourcePreparation(creating));

        var replacement = InvokePreparedGuaranteedResource(
            PrepareGuaranteedResourceInputs(
                fixture,
                scenario.OperationKey + "_replacement",
                scenario.RouteId));
        Assert.True(replacement.IsValid, DescribeIssues(replacement.Issues));
    }

    [Fact]
    public void ResourcePreparation_ExactRetryRollbackDoesNotReleaseExistingReservation()
    {
        var scenario = CreateGuaranteedResourceRegistryScenario(
            quantity: 2,
            includeAlternateRoute: false);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var prepared = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var creating = InvokePreparedGuaranteedResource(prepared);
        var retry = InvokePreparedGuaranteedResource(prepared);
        Assert.True(creating.IsValid, DescribeIssues(creating.Issues));
        Assert.True(retry.IsValid, DescribeIssues(retry.Issues));
        Assert.Same(creating.Authority, retry.Authority);

        Assert.True(RollbackNewResourcePreparation(retry));

        AssertInvalidResourcePreparation(
            InvokePreparedGuaranteedResource(PrepareGuaranteedResourceInputs(
                fixture,
                scenario.OperationKey + "_competing",
                scenario.RouteId)),
            "mortal_wound_treatment_resource_reservation_overbooked");
    }

    [Fact]
    public void ResourcePreparation_StaleOwnershipCannotReleaseRecreatedReservation()
    {
        var scenario = CreateGuaranteedResourceRegistryScenario(
            quantity: 2,
            includeAlternateRoute: false);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var prepared = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var original = InvokePreparedGuaranteedResource(prepared);
        Assert.True(original.IsValid, DescribeIssues(original.Issues));
        Assert.True(RollbackNewResourcePreparation(original));

        var recreated = InvokePreparedGuaranteedResource(prepared);
        Assert.True(recreated.IsValid, DescribeIssues(recreated.Issues));
        Assert.NotSame(original.Authority, recreated.Authority);
        Assert.Equal(original.Authority!.ReservationId, recreated.Authority!.ReservationId);
        Assert.Equal(
            original.Authority.AuthorityFingerprint,
            recreated.Authority.AuthorityFingerprint);

        Assert.False(RollbackNewResourcePreparation(original));
        AssertInvalidResourcePreparation(
            InvokePreparedGuaranteedResource(PrepareGuaranteedResourceInputs(
                fixture,
                scenario.OperationKey + "_competing",
                scenario.RouteId)),
            "mortal_wound_treatment_resource_reservation_overbooked");
    }

    [Fact]
    public void ResourcePreparation_AcceptedStateRebindInvalidatesHeldReservations()
    {
        var scenario = CreateGuaranteedResourceRegistryScenario(
            quantity: 2,
            includeAlternateRoute: false);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var oldPreparation = InvokePreparedGuaranteedResource(
            PrepareGuaranteedResourceInputs(
                fixture,
                scenario.OperationKey + "_old",
                scenario.RouteId));
        Assert.True(oldPreparation.IsValid, DescribeIssues(oldPreparation.Issues));

        fixture.SetCanonicalPlayerHealthForRequirementTest(9);

        var rebound = InvokePreparedGuaranteedResource(
            PrepareGuaranteedResourceInputs(
                fixture,
                scenario.OperationKey + "_rebound",
                scenario.RouteId));
        Assert.True(rebound.IsValid, DescribeIssues(rebound.Issues));
        Assert.False(RollbackNewResourcePreparation(oldPreparation));
    }

    [Fact]
    public async Task ResourcePreparation_GenerationRotationInvalidatesHeldReservations()
    {
        var scenario = CreateGuaranteedResourceRegistryScenario(
            quantity: 2,
            includeAlternateRoute: false);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var oldPreparation = InvokePreparedGuaranteedResource(
            PrepareGuaranteedResourceInputs(
                fixture,
                scenario.OperationKey + "_old_generation",
                scenario.RouteId));
        Assert.True(oldPreparation.IsValid, DescribeIssues(oldPreparation.Issues));

        await fixture.RotateGenerationAndPrepareFreshSnapshotAsync(
            "t068a_resource_registry_generation_rotation");

        var rotated = InvokePreparedGuaranteedResource(
            PrepareGuaranteedResourceInputs(
                fixture,
                scenario.OperationKey + "_rotated_generation",
                scenario.RouteId));
        Assert.True(rotated.IsValid, DescribeIssues(rotated.Issues));
        Assert.NotEqual(
            oldPreparation.Authority!.AcceptedStateFingerprint,
            rotated.Authority!.AcceptedStateFingerprint);
        Assert.False(RollbackNewResourcePreparation(oldPreparation));
    }

    [Fact]
    public void ResourcePreparation_NotRequiredRollbackReleasesOnlyCreatingAgreement()
    {
        var scenario = CreateTwoZeroClaimGuaranteedRoutesScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var changed = PrepareGuaranteedResourceInputs(
            fixture,
            scenario.OperationKey,
            AlternateGuaranteedRouteId);
        var creating = InvokePreparedGuaranteedResource(first);
        var retry = InvokePreparedGuaranteedResource(first);
        Assert.True(creating.IsValid, DescribeIssues(creating.Issues));
        Assert.True(retry.IsValid, DescribeIssues(retry.Issues));
        Assert.Equal("not_required", creating.Authority!.ReservationDisposition);
        Assert.Same(creating.Authority, retry.Authority);

        Assert.True(RollbackNewResourcePreparation(retry));
        AssertInvalidResourcePreparation(
            InvokePreparedGuaranteedResource(changed),
            "mortal_wound_treatment_resource_reservation_conflict");
        Assert.True(RollbackNewResourcePreparation(creating));

        var changedAfterRollback = InvokePreparedGuaranteedResource(changed);
        Assert.True(
            changedAfterRollback.IsValid,
            DescribeIssues(changedAfterRollback.Issues));
        Assert.Equal(
            "not_required",
            changedAfterRollback.Authority!.ReservationDisposition);
    }

    private const string AlternateGuaranteedRouteId =
        "guaranteed_t068a_alternate_resource_route";

    private static ResolverScenario CreateGuaranteedResourceRegistryScenario(
        int quantity,
        bool includeAlternateRoute)
    {
        var source = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        var before = source.Before.DeepClone().AsObject();
        var route = before["treatment"]!["routes"]![0]!.AsObject();
        route["requirements"]!.AsArray().Add(new JsonObject
        {
            ["kind"] = "item_quantity",
            ["itemRef"] = "sterile_thread",
            ["quantity"] = quantity,
            ["ownerRole"] = "provider"
        });
        route["resourcePolicy"]!["mutations"] = new JsonArray(new JsonObject
        {
            ["kind"] = "consume_requirement",
            ["scope"] = "common",
            ["milestoneOrdinal"] = null,
            ["requirementIndex"] = 1
        });
        if (includeAlternateRoute)
        {
            var alternate = route.DeepClone().AsObject();
            alternate["routeId"] = AlternateGuaranteedRouteId;
            before["treatment"]!["routes"]!.AsArray().Add(alternate);
            before["treatment"]!["knownRouteIds"]!.AsArray().Add(
                AlternateGuaranteedRouteId);
        }
        return source with { Before = before };
    }

    private static ResolverScenario CreateTwoZeroClaimGuaranteedRoutesScenario()
    {
        var source = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        var before = source.Before.DeepClone().AsObject();
        var alternate = before["treatment"]!["routes"]![0]!.DeepClone().AsObject();
        alternate["routeId"] = AlternateGuaranteedRouteId;
        before["treatment"]!["routes"]!.AsArray().Add(alternate);
        before["treatment"]!["knownRouteIds"]!.AsArray().Add(
            AlternateGuaranteedRouteId);
        return source with { Before = before };
    }

    private static PreparedGuaranteedResourceInputs PrepareGuaranteedResourceInputs(
        AcceptedStateFixture fixture,
        string operationKey,
        string routeId)
    {
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var before = fixture.ReadCurrentWound();
        var coordinatesResult = MortalWoundTreatmentPlanner.CreateAttemptCoordinates(
            acceptedState,
            before,
            operationKey,
            routeId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.True(coordinatesResult.IsValid, DescribeIssues(coordinatesResult.Issues));
        var coordinates = Assert.IsType<MortalWoundTreatmentAttemptCoordinates>(
            coordinatesResult.Coordinates);
        var route = Assert.IsType<MortalWoundGuaranteedRouteDefinition>(Assert.Single(
            acceptedState.TreatmentDefinition.Routes,
            candidate => string.Equals(
                candidate.RouteId,
                routeId,
                StringComparison.Ordinal)));
        var bundleResult =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForGuaranteed(
                acceptedState,
                coordinates,
                before);
        Assert.True(bundleResult.IsValid, DescribeIssues(bundleResult.Issues));
        var bundle = Assert.IsType<MortalWoundTreatmentRequirementAuthorityBundle>(
            bundleResult.Authority);
        var proofResult = MortalWoundTreatmentCapabilityAuthority.ExportCurrent(
            acceptedState,
            coordinates,
            route.Resolution.CapabilityRef,
            route.Resolution.ActorRole);
        Assert.True(proofResult.IsValid, DescribeIssues(proofResult.Issues));
        var proof = Assert.IsType<MortalWoundTreatmentCapabilityProof>(
            proofResult.Proof);
        return new PreparedGuaranteedResourceInputs(
            acceptedState,
            coordinates,
            before,
            bundle,
            proof);
    }

    private static PreparedCourseResourceInputs PrepareCourseResourceInputs(
        AcceptedStateFixture fixture,
        ResolverScenario scenario,
        string operationKey)
    {
        var probe = InspectCourseMode(fixture, operationKey, scenario.RouteId);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            probe.AcceptedState);
        var coordinates = Assert.IsType<MortalWoundTreatmentAttemptCoordinates>(
            probe.Coordinates);
        var courseAuthority = Assert.IsType<MortalWoundCourseModeAuthority>(
            AssertReadyCourseModeAuthority(probe.Result, scenario));
        var bundleResult =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForCourseMilestone(
                acceptedState,
                coordinates,
                probe.Before,
                probe.History,
                courseAuthority);
        Assert.Equal("Satisfied", bundleResult.Status);
        var bundle = Assert.IsType<MortalWoundTreatmentRequirementAuthorityBundle>(
            bundleResult.Authority);
        return new PreparedCourseResourceInputs(
            acceptedState,
            coordinates,
            probe.Before,
            bundle,
            courseAuthority);
    }

    private static MortalWoundTreatmentResourcePreparationResult
        InvokePreparedGuaranteedResource(PreparedGuaranteedResourceInputs prepared) =>
        MortalWoundTreatmentResourceComposer.PrepareGuaranteed(
            prepared.AcceptedState,
            prepared.Coordinates,
            prepared.Before,
            prepared.RequirementAuthority,
            prepared.CapabilityProof);

    private static void AssertInvalidResourcePreparation(
        MortalWoundTreatmentResourcePreparationResult result,
        string expectedCode)
    {
        Assert.False(result.IsValid);
        Assert.Null(result.Authority);
        Assert.Contains(result.Issues, issue => string.Equals(
            issue.Code,
            expectedCode,
            StringComparison.Ordinal));
    }

    private static bool RollbackNewResourcePreparation(
        MortalWoundTreatmentResourcePreparationResult preparation)
    {
        var rollback = typeof(MortalWoundTreatmentResourceComposer).GetMethods(
                BindingFlags.Static | BindingFlags.NonPublic)
            .SingleOrDefault(method =>
                string.Equals(method.Name, "RollbackNew", StringComparison.Ordinal) &&
                method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType ==
                    typeof(MortalWoundTreatmentResourcePreparationResult));
        Assert.NotNull(rollback);
        Assert.Equal(typeof(bool), rollback!.ReturnType);
        return Assert.IsType<bool>(rollback.Invoke(null, new object?[] { preparation }));
    }

    private static ResolverScenario CreateFirstCourseResourceScenario()
    {
        var scenario = ConfigureCourseConsequenceEnvelope(
            CreateScenario(
                "course_first_milestone_is_ready_at_inclusive_due_time",
                "course"),
            removeOwnedComplicationBeforeReduction: true);
        var before = scenario.Before.DeepClone().AsObject();
        var route = before["treatment"]!["routes"]![0]!.AsObject();
        route["requirements"] = new JsonArray(new JsonObject
        {
            ["kind"] = "item_quantity",
            ["itemRef"] = "sterile_thread",
            ["quantity"] = 1,
            ["ownerRole"] = "provider"
        });
        var outcomes = route["outcomes"]!.AsArray();
        outcomes[0]!["completion"] = "active";
        var second = CourseMilestone(
            2,
            480,
            "active",
            new JsonArray(new JsonObject { ["kind"] = "stabilize" }));
        second["requirements"]![0]!["itemRef"] = "future_course_dose_2";
        outcomes.Add(second);
        var third = CourseMilestone(
            3,
            960,
            "completed",
            new JsonArray(new JsonObject
            {
                ["kind"] = "heal",
                ["legacies"] = new JsonArray()
            }));
        third["requirements"]![0]!["itemRef"] = "future_course_dose_3";
        outcomes.Add(third);
        route["resourcePolicy"]!["mutations"] = new JsonArray(
            new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "common",
                ["milestoneOrdinal"] = null,
                ["requirementIndex"] = 0
            },
            CourseMutation(1),
            CourseMutation(2),
            CourseMutation(3));
        return scenario with { Before = before };
    }

    private static object AssertValidResourcePreparation(object result)
    {
        AssertClosedProperties(result, new[] { "IsValid", "Issues", "Authority" });
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")),
            DescribeIssues(AsObjects(ReadRequiredProperty(result, "Issues"))
                .Select(Assert.IsType<ValidationIssue>)));
        var issues = ReadRequiredProperty(result, "Issues");
        Assert.Empty(AsObjects(issues));
        AssertFrozenSequence(issues, allowEmptyArray: true);
        return ReadRequiredProperty(result, "Authority");
    }

    private sealed record PreparedGuaranteedResourceInputs(
        MortalWoundTreatmentAcceptedStateAuthority AcceptedState,
        MortalWoundTreatmentAttemptCoordinates Coordinates,
        WoundMaterializationEnvelope Before,
        MortalWoundTreatmentRequirementAuthorityBundle RequirementAuthority,
        MortalWoundTreatmentCapabilityProof CapabilityProof);

    private sealed record PreparedCourseResourceInputs(
        MortalWoundTreatmentAcceptedStateAuthority AcceptedState,
        MortalWoundTreatmentAttemptCoordinates Coordinates,
        WoundMaterializationEnvelope Before,
        MortalWoundTreatmentRequirementAuthorityBundle RequirementAuthority,
        MortalWoundCourseModeAuthority CourseAuthority);

    private static MethodInfo RequireResourcePreparationMethod(
        string name,
        params Type[] parameterTypes)
    {
        var composer = typeof(WoundMaterializationContract).Assembly.GetType(
            TreatmentResourceComposerTypeName,
            throwOnError: false,
            ignoreCase: false);
        Assert.True(
            composer is not null,
            $"T068-A requires the production {TreatmentResourceComposerTypeName} surface.");
        var method = Assert.Single(composer!.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal) &&
                         candidate.GetParameters().Length == parameterTypes.Length);
        Assert.Equal(
            parameterTypes,
            method.GetParameters().Select(static parameter => parameter.ParameterType));
        return method;
    }
}
