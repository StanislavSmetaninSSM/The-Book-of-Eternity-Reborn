using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void ProcedureCheckAuthority_NullFateAgreementSurvivesRetryUntilAtomicRelease()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 1);
        using var fixture = AcceptedStateFixture.Create(scenario);

        var first = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey + "_first",
            scenario.RouteId);
        var firstAuthority = AssertValidProcedureCheckAuthority(first);
        var secondAuthority = AssertValidProcedureCheckAuthority(
            InvokeProcedureCheckAuthority(
                fixture,
                scenario.OperationKey + "_second",
                scenario.RouteId));
        var third = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey + "_third",
            scenario.RouteId);
        var thirdAuthority = AssertValidProcedureCheckAuthority(third);

        Assert.Equal(
            "effect_fate_shield_older",
            ReadPreparedFateEffectIdFromAuthority(firstAuthority));
        Assert.Equal(
            "effect_fate_shield_newer",
            ReadPreparedFateEffectIdFromAuthority(secondAuthority));
        Assert.Null(ReadPropertyAllowingNull(
            thirdAuthority,
            "PreparedCriticalReaction"));

        var release = ExactInstanceMethod(
            firstAuthority.GetType(),
            "ReleaseProvisionalReservations",
            1);
        Assert.True(Assert.IsType<bool>(InvokeInstance(
            release,
            firstAuthority,
            new object?[] { first.AcceptedState })));

        var thirdRetry = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey + "_third",
            scenario.RouteId);
        var thirdRetryAuthority = AssertValidProcedureCheckAuthority(thirdRetry);
        Assert.Equal(
            CanonicalValue(thirdAuthority),
            CanonicalValue(thirdRetryAuthority));
        Assert.Null(ReadPropertyAllowingNull(
            thirdRetryAuthority,
            "PreparedCriticalReaction"));

        Assert.True(Assert.IsType<bool>(InvokeInstance(
            release,
            thirdRetryAuthority,
            new object?[] { thirdRetry.AcceptedState })));
        var thirdAfterRelease = AssertValidProcedureCheckAuthority(
            InvokeProcedureCheckAuthority(
                fixture,
                scenario.OperationKey + "_third",
                scenario.RouteId));
        Assert.Equal(
            "effect_fate_shield_older",
            ReadPreparedFateEffectIdFromAuthority(thirdAfterRelease));
    }
}
