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
    public void Replay_RequiresProductionAppendBeforeProbeCanExerciseDetachedExactReplay()
    {
        var coordinator = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentPersistenceCoordinator",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(coordinator is not null,
            "T067-B must add the sole production-owned MortalWoundTreatmentPersistenceCoordinator.AppendResolvedAttempt " +
            "before T061 can construct durable treatment history without a test-authored row.");

        var append = ExactStaticMethod(coordinator!, "AppendResolvedAttempt", 4);
        Assert.Equal("MortalWoundTreatmentAppendResult", append.ReturnType.Name);
        Assert.Equal("FileSystemManager", append.GetParameters()[0].ParameterType.Name);
        Assert.Equal("CanonicalWriteLease", append.GetParameters()[1].ParameterType.Name);
        Assert.Equal("WoundAcceptedTurnBinding", append.GetParameters()[2].ParameterType.Name);
        Assert.Equal("MortalWoundTreatmentResolution", append.GetParameters()[3].ParameterType.Name);
        AssertClosedResultType(append.ReturnType, "History");
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
