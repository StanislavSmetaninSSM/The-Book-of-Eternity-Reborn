using System.Reflection;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// T061 restart/replay boundary.  It deliberately has no local treatment-history row:
/// only the future client-owned append coordinator may persist a resolved attempt.
/// </summary>
public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void Replay_RequiresTheCommonT070PublicationPlanBeforeProbeCanExerciseDetachedExactReplay()
    {
        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.WoundAcceptedTurnPlanner",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(planner is not null,
            "T070 must compose a treatment publication through the common accepted-plan planner; T061 never appends history directly.");

        var compose = ExactStaticMethod(planner!, "ComposeMortalWoundTreatmentPublication", 6);
        Assert.Equal("FileSystemManager", compose.GetParameters()[0].ParameterType.Name);
        Assert.Equal("CanonicalWriteLease", compose.GetParameters()[1].ParameterType.Name);
        Assert.Equal("GameResponse", compose.GetParameters()[2].ParameterType.Name);
        Assert.Equal("MortalWoundTreatmentAcceptedStateAuthority", compose.GetParameters()[3].ParameterType.Name);
        Assert.Equal("MortalWoundTreatmentAttemptRequest", compose.GetParameters()[4].ParameterType.Name);
        Assert.Equal("MortalWoundTreatmentResolution", compose.GetParameters()[5].ParameterType.Name);
        Assert.DoesNotContain(compose.GetParameters(), static parameter =>
            typeof(System.Text.Json.Nodes.JsonNode).IsAssignableFrom(parameter.ParameterType) ||
            parameter.ParameterType.Name.Contains("History", StringComparison.Ordinal) ||
            parameter.ParameterType.Name.Contains("Receipt", StringComparison.Ordinal));
        AssertClosedResultType(compose.ReturnType, "Plan");
    }

    [Fact]
    public void ReplayProbe_ExactContractKeepsNoIntentAndReturnsOnlyDetachedRequestAndReceipt()
    {
        var probe = ExactInstanceMethod(typeof(WoundHistoryParseResult), "ProbeTreatmentAttempt", 3);
        Assert.Equal("MortalWoundTreatmentReplayProbeResult", probe.ReturnType.Name);
        Assert.Equal(typeof(string), probe.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(string), probe.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(string), probe.GetParameters()[2].ParameterType);

        Assert.Equal(
            new[] { "Status", "Issues", "Request", "Receipt" }.OrderBy(static value => value),
            probe.ReturnType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static value => value));
    }
}
