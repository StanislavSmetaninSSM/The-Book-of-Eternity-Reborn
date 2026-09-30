using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundRecoveryTests
{
    /// <summary>
    /// Rejects changed recovery resolutions before a common plan is
    /// registered, preserving all canonical bytes and the original planner result.
    /// </summary>
    [Fact]
    public void Compose_RejectsChangedResolutionWithoutPlanOrWrites()
    {
        using var fixture = Fixture.Create(Scenario.AtDueBoundary());
        var original = Assert.IsType<MortalWoundRecoveryResolution>(
            Required(InvokePlan(fixture), "Resolution"));
        var type = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundRecoveryAcceptedPlanComposer",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(type is not null,
            "Recovery composition must authenticate a genuine planner result before publication.");
        var compose = ExactStatic(type!, "Compose", 4);
        var before = fixture.CaptureCanonicalTreeBytes();
        var candidates = new[]
        {
            original with { ElapsedCadences = original.ElapsedCadences + 1 },
            original with { TransitionIntents = Array.Empty<IMortalWoundRecoveryTransitionIntent>() }
        };

        foreach (var candidate in candidates)
        {
            var result = Invoke(compose, fixture.FileSystem, fixture.Lease,
                fixture.Binding, candidate);
            AssertClosed(result, "AcceptedPlan", "Disposition", "Issues", "Receipt", "WoundStageBundle");
            Assert.Equal("Rejected", Convert.ToString(Required(result, "Disposition")));
            Assert.NotEmpty(Values(result, "Issues"));
            Assert.Null(Optional(result, "AcceptedPlan"));
            Assert.Null(Optional(result, "WoundStageBundle"));
            Assert.Null(Optional(result, "Receipt"));
            fixture.AssertCanonicalTreeBytesUnchanged(before);
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
                fixture.FileSystem, fixture.Lease, out _, out _));
        }

        var staleBinding = fixture.Binding with { Turn = fixture.Binding.Turn + 1 };
        var staleResult = Invoke(compose, fixture.FileSystem, fixture.Lease,
            staleBinding, original);
        Assert.Equal("Rejected", Convert.ToString(Required(staleResult, "Disposition")));
        Assert.NotEmpty(Values(staleResult, "Issues"));
        Assert.Null(Optional(staleResult, "AcceptedPlan"));
        Assert.Null(Optional(staleResult, "Receipt"));
        Assert.Null(Optional(staleResult, "WoundStageBundle"));
        fixture.AssertCanonicalTreeBytesUnchanged(before);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem, fixture.Lease, out _, out _));

        Assert.Equal(1, original.ElapsedCadences);
        Assert.Single(original.TransitionIntents);
    }
}
