using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void TreatmentCommandWriteIngress_ParseRejectsExactDuplicateTreatmentRoot()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var command = ComposeTreatmentCommand(
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_write_duplicate",
                scenario.RouteId),
            "One accepted treatment may be persisted only once.");
        var duplicated = command.Root.DeepClone().AsObject();
        var rows = duplicated["commands"]!.AsArray();
        rows.Add(Assert.Single(rows)!.DeepClone());

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(duplicated));

        Assert.False(parsed.Success);
        Assert.Contains(parsed.Issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_persisted_coordinate_collision",
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("operation", "exact")]
    [InlineData("operation", "confusable")]
    [InlineData("attempt", "exact")]
    [InlineData("attempt", "confusable")]
    [InlineData("event", "exact")]
    [InlineData("event", "confusable")]
    [InlineData("course", "exact")]
    [InlineData("course", "confusable")]
    [InlineData("fingerprint", "exact")]
    public void TreatmentCommandWriteIngress_CoordinateGateRejectsCollision(
        string axis,
        string collisionKind)
    {
        var first = TreatmentWriteCoordinate(
            "operation_A",
            "attempt_A",
            "event_A",
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            axis == "course" ? "course_A" : null,
            axis == "course" ? 2 : null);
        var second = TreatmentWriteCoordinate(
            "operation_B",
            "attempt_B",
            "event_B",
            "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            axis == "course" ? "course_B" : null,
            axis == "course" ? 2 : null);
        var replacement = collisionKind == "exact" ? axis switch
        {
            "operation" => first.OperationKey,
            "attempt" => first.AttemptId,
            "event" => first.EventRef,
            "course" => first.CourseId!,
            "fingerprint" => first.RequestFingerprint,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        } : axis switch
        {
            "operation" => "operation_\u0410",
            "attempt" => "attempt_\u0410",
            "event" => "event_\u0410",
            "course" => "course_\u0410",
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        };
        second = axis switch
        {
            "operation" => second with { OperationKey = replacement },
            "attempt" => second with { AttemptId = replacement },
            "event" => second with { EventRef = replacement },
            "course" => second with { CourseId = replacement },
            "fingerprint" => second with { RequestFingerprint = replacement },
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        };

        Assert.False(HasUniqueTreatmentWriteCoordinates(new[] { first, second }));
    }

    [Fact]
    public void TreatmentCommandWriteIngress_RecomposeAcceptsCanonicalRoot()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var command = ComposeTreatmentCommand(
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_write_positive",
                scenario.RouteId),
            "The canonical accepted treatment remains writable.");
        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(command.Root));

        Assert.True(parsed.Success, DescribeIssues(parsed.Issues));

        var recomposed = MortalWoundTreatmentCommandCodec.RecomposeRoot(
            command.Binding,
            parsed.TreatmentCommands);

        Assert.NotNull(recomposed);
        Assert.True(JsonNode.DeepEquals(command.Root, recomposed));
        Assert.True(HasUniqueTreatmentWriteCoordinates(new[]
        {
            TreatmentWriteCoordinate(
                "operation_A",
                "attempt_A",
                "event_A",
                "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "course_A",
                2),
            TreatmentWriteCoordinate(
                "operation_B",
                "attempt_B",
                "event_B",
                "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "course_B",
                2)
        }));
    }

    private static TreatmentWriteCoordinateProbe TreatmentWriteCoordinate(
        string operationKey,
        string attemptId,
        string eventRef,
        string requestFingerprint,
        string? courseId,
        int? courseMilestoneOrdinal) => new(
        operationKey,
        attemptId,
        eventRef,
        requestFingerprint,
        courseId,
        courseMilestoneOrdinal);

    private static bool HasUniqueTreatmentWriteCoordinates(
        IReadOnlyList<TreatmentWriteCoordinateProbe> coordinates)
    {
        var projected = coordinates
            .Select(static value => (
                value.OperationKey,
                value.AttemptId,
                value.EventRef,
                value.RequestFingerprint,
                value.CourseId,
                value.CourseMilestoneOrdinal))
            .ToArray();
        return MortalWoundTreatmentCommandCodec.HasUniquePersistedCoordinates(
            projected);
    }

    private sealed record TreatmentWriteCoordinateProbe(
        string OperationKey,
        string AttemptId,
        string EventRef,
        string RequestFingerprint,
        string? CourseId,
        int? CourseMilestoneOrdinal);
}
