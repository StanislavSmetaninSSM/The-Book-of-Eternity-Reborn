using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void ProcedureCheckAuthority_ReviewCompoundReservationRejectsUntrustedActorInputs()
    {
        var reserve = typeof(MortalWoundTreatmentAcceptedStateAuthority).GetMethod(
            "ReserveProcedureReservations",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var parameters = reserve.GetParameters();

        Assert.Equal(5, parameters.Length);
        Assert.Equal(typeof(object), parameters[0].ParameterType);
        Assert.Equal(
            "reservationCapability",
            parameters[0].Name,
            ignoreCase: false);

        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 17);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var prepared = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var rejected = Assert.IsType<MortalWoundProcedureReservationSetResult>(
            InvokeInstance(
                reserve,
                prepared.AcceptedState,
                new object?[]
                {
                    new object(),
                    prepared.Coordinates,
                    "normal",
                    "npc",
                    "npc_forged"
                }));
        Assert.False(rejected.IsValid);
        Assert.Null(rejected.DiceReservation);

        var authority = AssertValidProcedureCheckAuthority(
            InvokeProcedureCheckAuthority(
                fixture,
                scenario.OperationKey,
                scenario.RouteId));
        Assert.Equal(
            new[] { 0 },
            ReadIntSequence(ReadRequiredProperty(authority, "SourceIndices")));
        Assert.Equal(
            "effect_fate_shield_older",
            ReadPreparedFateEffectIdFromAuthority(authority));
    }

    [Fact]
    public void ProcedureCheckAuthority_ReviewNullableFateOwnershipBindsExactAgreementIdentity()
    {
        Assert.Contains(
            typeof(MortalWoundProcedureReservationSetResult).GetProperties(),
            property => property.Name == "CriticalReactionAgreement");
        Assert.Contains(
            typeof(MortalWoundProcedureReservationOwnership).GetProperties(
                BindingFlags.Instance | BindingFlags.NonPublic),
            property => property.Name == "CriticalReactionAgreement");

        var matches = typeof(MortalWoundProcedureReservationOwnership).GetMethod(
            "Matches",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Contains(
            matches.GetParameters(),
            parameter => parameter.ParameterType.Name ==
                "MortalWoundCriticalReactionReservationAgreement");
        var releaseAgreement = typeof(MortalWoundCriticalReactionReservationRegistry)
            .GetMethod(
                "MatchesReleaseAgreement",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Contains(
            releaseAgreement.GetParameters(),
            parameter => parameter.ParameterType.Name ==
                "MortalWoundCriticalReactionReservationAgreement");

        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 1);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var capability = ProcedureReservationCapabilityForTest();

        var first = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey + "_first",
            scenario.RouteId);
        var second = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey + "_second",
            scenario.RouteId);
        var third = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey + "_third",
            scenario.RouteId);
        Assert.NotNull(first.AcceptedState.ReserveProcedureReservations(
            capability,
            first.Coordinates,
            "normal",
            "player",
            "player_current").CriticalReactionReservation);
        Assert.NotNull(second.AcceptedState.ReserveProcedureReservations(
            capability,
            second.Coordinates,
            "normal",
            "player",
            "player_current").CriticalReactionReservation);

        var diceOnly = third.AcceptedState.ReserveProcedureReservations(
            capability,
            third.Coordinates,
            "normal",
            "npc",
            "npc_forged");
        Assert.True(diceOnly.IsValid, DescribeIssues(diceOnly.Issues));
        Assert.Null(diceOnly.CriticalReactionAgreement);
        var oldNull = third.AcceptedState.ReserveProcedureReservations(
            capability,
            third.Coordinates,
            "normal",
            "player",
            "player_current");
        Assert.NotNull(oldNull.CriticalReactionAgreement);
        Assert.Null(oldNull.CriticalReactionReservation);
        Assert.False(oldNull.Ownership!.DiceWasCreated);
        Assert.True(oldNull.Ownership.CriticalReactionWasCreated);
        Assert.True(third.AcceptedState.RollbackNewProcedureReservations(
            oldNull.DiceReservation!,
            oldNull.CriticalReactionReservation,
            oldNull.CriticalReactionAgreement,
            oldNull.Ownership));

        var newNull = third.AcceptedState.ReserveProcedureReservations(
            capability,
            third.Coordinates,
            "normal",
            "player",
            "player_current");
        Assert.NotNull(newNull.CriticalReactionAgreement);
        Assert.NotSame(
            oldNull.CriticalReactionAgreement,
            newNull.CriticalReactionAgreement);
        Assert.False(third.AcceptedState.RollbackNewProcedureReservations(
            oldNull.DiceReservation!,
            oldNull.CriticalReactionReservation,
            oldNull.CriticalReactionAgreement,
            oldNull.Ownership));
        var retry = third.AcceptedState.ReserveProcedureReservations(
            capability,
            third.Coordinates,
            "normal",
            "player",
            "player_current");
        Assert.Same(
            newNull.CriticalReactionAgreement,
            retry.CriticalReactionAgreement);
    }

    [Fact]
    public void ProcedureCheckAuthority_ReviewRegistryUsesPureAdmissionAndOnlyCompoundRoute()
    {
        var registrySource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "AcceptedTurnAuthorityRegistry.cs"));

        Assert.DoesNotContain(
            "coordinates.MatchesAcceptedState(acceptedState)",
            registrySource,
            StringComparison.Ordinal);
        Assert.NotNull(typeof(MortalWoundTreatmentAttemptCoordinates).GetMethod(
            "AgreesWithAcceptedStateSemantics",
            BindingFlags.Instance | BindingFlags.NonPublic));

        Assert.DoesNotContain(
            typeof(AcceptedTurnAuthorityRegistry).GetMethods(
                BindingFlags.Static | BindingFlags.NonPublic),
            method => method.Name is "ReserveMortalWoundProcedureDice" or
                "ReleaseMortalWoundProcedureDice");
        Assert.DoesNotContain(
            typeof(MortalWoundTreatmentAcceptedStateAuthority).GetMethods(
                BindingFlags.Instance | BindingFlags.NonPublic),
            method => method.Name is "ReserveProcedureDice" or "ReleaseProcedureDice");
        var state = Assert.Single(
            typeof(AcceptedTurnAuthorityRegistry).GetNestedTypes(BindingFlags.NonPublic),
            type => type.Name == "AcceptedTurnAuthorityState");
        Assert.DoesNotContain(
            state.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic),
            method => method.Name is "ReserveMortalWoundProcedureDice" or
                "ReleaseMortalWoundProcedureDice");
    }

    [Fact]
    public void ProcedureCheckAuthority_ReviewDiceClaimBindsFrozenAcceptedPoolAgreement()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(3, 7, 11);
        using var fixture = AcceptedStateFixture.Create(scenario);

        var invocation = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var authority = AssertValidProcedureCheckAuthority(invocation);
        var reservation = Assert.IsType<MortalWoundProcedureDiceReservation>(
            authority.GetType().GetField(
                "_diceReservation",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(authority));

        Assert.Equal(
            new[]
            {
                "AcceptedStateFingerprint",
                "AttemptId",
                "ClaimFingerprint",
                "CoordinatesFingerprint",
                "OperationKey",
                "PoolFingerprint",
                "RollMode",
                "SourceIndices",
                "SourceRolls"
            },
            reservation.GetType().GetProperties(
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(static property => property.Name)
                .OrderBy(static name => name));

        var binding = invocation.AcceptedState.Binding;
        var poolFields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.accepted_d20_pool",
            "1",
            invocation.AcceptedState.SessionGeneration,
            binding.SessionId,
            binding.RequestId,
            binding.SnapshotToken,
            binding.Turn.ToString(CultureInfo.InvariantCulture),
            "3",
            "0", "3",
            "1", "7",
            "2", "11"
        };
        var expectedPoolFingerprint =
            WoundAcceptedTurnFingerprintWriter.Compute(poolFields);
        Assert.Equal(
            expectedPoolFingerprint,
            reservation.PoolFingerprint);

        var claimFields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.procedure_dice_claim",
            "1",
            expectedPoolFingerprint,
            invocation.AcceptedState.AcceptedStateFingerprint,
            invocation.Coordinates.OperationKey,
            invocation.Coordinates.AttemptId,
            invocation.Coordinates.CoordinatesFingerprint,
            "normal",
            "1",
            "0", "3"
        };
        Assert.Equal(
            WoundAcceptedTurnFingerprintWriter.Compute(claimFields),
            reservation.ClaimFingerprint);
        Assert.Equal(
            invocation.Coordinates.AttemptId,
            reservation.AttemptId);
        Assert.Equal(new[] { 0 }, reservation.SourceIndices);
        Assert.Equal(new[] { 3 }, reservation.SourceRolls);
        Assert.IsType<ReadOnlyCollection<int>>(reservation.SourceIndices);
        Assert.IsType<ReadOnlyCollection<int>>(reservation.SourceRolls);

        Assert.DoesNotContain(
            typeof(MortalWoundTreatmentAcceptedStateAuthority).GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            property => property.Name.Contains("D20", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Dice", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProcedureCheckAuthority_ReviewExactRetryRollbackCannotReleaseEstablishedDiceAndFateClaims()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 1);
        using var fixture = AcceptedStateFixture.Create(scenario);

        var firstInputs = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey + "_first",
            scenario.RouteId);
        var first = firstInputs.AcceptedState.ReserveProcedureReservations(
            ProcedureReservationCapabilityForTest(),
            firstInputs.Coordinates,
            "normal",
            "player",
            "player_current");
        Assert.True(first.IsValid, DescribeIssues(first.Issues));
        Assert.NotNull(first.DiceReservation);
        Assert.NotNull(first.Ownership);
        Assert.True(first.Ownership.DiceWasCreated);
        Assert.True(first.Ownership.CriticalReactionWasCreated);
        Assert.Equal(
            "effect_fate_shield_older",
            first.CriticalReactionReservation!.EffectId);

        var retry = firstInputs.AcceptedState.ReserveProcedureReservations(
            ProcedureReservationCapabilityForTest(),
            firstInputs.Coordinates,
            "normal",
            "player",
            "player_current");
        Assert.True(retry.IsValid, DescribeIssues(retry.Issues));
        Assert.Same(first.DiceReservation, retry.DiceReservation);
        Assert.Same(
            first.CriticalReactionReservation,
            retry.CriticalReactionReservation);
        Assert.NotNull(retry.Ownership);
        Assert.False(retry.Ownership.DiceWasCreated);
        Assert.False(retry.Ownership.CriticalReactionWasCreated);
        Assert.True(firstInputs.AcceptedState.RollbackNewProcedureReservations(
            retry.DiceReservation!,
            retry.CriticalReactionReservation,
            retry.CriticalReactionAgreement,
            retry.Ownership));

        var rollback = typeof(MortalWoundTreatmentAcceptedStateAuthority).GetMethod(
            "RollbackNewProcedureReservations",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.DoesNotContain(
            rollback.GetParameters(),
            parameter => parameter.ParameterType == typeof(bool));
        Assert.All(
            typeof(MortalWoundProcedureReservationOwnership).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            constructor => Assert.True(constructor.IsPrivate));

        var exactRetry = firstInputs.AcceptedState.ReserveProcedureReservations(
            ProcedureReservationCapabilityForTest(),
            firstInputs.Coordinates,
            "normal",
            "player",
            "player_current");
        Assert.Same(first.DiceReservation, exactRetry.DiceReservation);
        Assert.Same(
            first.CriticalReactionReservation,
            exactRetry.CriticalReactionReservation);

        var freshInputs = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey + "_fresh",
            scenario.RouteId);
        var fresh = firstInputs.AcceptedState.ReserveProcedureReservations(
            ProcedureReservationCapabilityForTest(),
            freshInputs.Coordinates,
            "normal",
            "player",
            "player_current");
        Assert.True(fresh.IsValid, DescribeIssues(fresh.Issues));
        Assert.Equal(
            new[] { 1 },
            fresh.DiceReservation!.SourceIndices);
        Assert.Equal(
            "effect_fate_shield_newer",
            fresh.CriticalReactionReservation!.EffectId);

        var authoritySource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "MortalWoundProcedureCheckAuthority.cs"));
        Assert.Contains(
            "acceptedState.RollbackNewProcedureReservations(",
            authoritySource,
            StringComparison.Ordinal);
    }

    private static object ProcedureReservationCapabilityForTest() =>
        typeof(MortalWoundProcedureCheckAuthority).GetField(
            "ProcedureReservationCapability",
            BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
}
