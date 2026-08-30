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
