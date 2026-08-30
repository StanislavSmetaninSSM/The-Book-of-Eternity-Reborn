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
