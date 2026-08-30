using System.Reflection;
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
