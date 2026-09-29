using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    /// <summary>
    /// Publishes two lawful course milestones and detaches their exact complete history prefix for replay fixtures.
    /// </summary>
    /// <returns>
    /// Immutable canonical history JSON after the owned fixture, leases and signing context have been disposed.
    /// </returns>
    internal static string CreatePublishedCourseReplayHistory()
    {
        var scenario = CreateScalarCoursePublicationScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var first = ResolveCurrentTreatment(
            fixture, "course", "operation_replay_prepared_course_start", scenario.RouteId);
        AssertCourseAcceptedResolution(first, 1, "active", interruption: false);
        ComposeAndPublishTreatment(fixture, first);

        fixture.RestartForReplay();
        fixture.PrepareNextTurn(43, 480, "replay_prepared_course_ordinal_2");
        var second = ResolveCurrentTreatment(
            fixture, "course", "operation_replay_prepared_course_ordinal_2", scenario.RouteId);
        AssertCourseAcceptedResolution(second, 2, "active", interruption: false);
        ComposeAndPublishTreatment(fixture, second);

        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(second.Request);
        var history = fixture.ReadCurrentHistory();
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        var accepted = Assert.Single(history.State!.Transitions,
            row => row.OperationKey == request.Coordinates.OperationKey);
        var result = Assert.IsType<MortalWoundTreatmentPersistedResult>(accepted.TransitionResult);
        Assert.Equal(request.RequestFingerprint, result.Request.RequestFingerprint);
        Assert.Equal(2, result.Receipt.CourseMilestoneOrdinal);
        Assert.Equal(3, history.State.Transitions.Count);
        Assert.Equal(new[] { 1, 2 }, history.State.Transitions
            .Where(row => row.CourseId == result.Receipt.CourseId)
            .Select(row => row.CourseMilestoneOrdinal!.Value)
            .ToArray());
        return WoundHistoryState.SerializeCanonical(history.State);
    }
}
